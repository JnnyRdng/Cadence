using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Core;

namespace Mobile;

[Service(
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone |
                            global::Android.Content.PM.ForegroundService.TypeConnectedDevice)]
public sealed class CadenceService : Service
{
    public const string ActionStart = "cadence.action.START";
    public const string ActionStop = "cadence.action.STOP";

    private const int NotificationId = 1;
    private const string ChannelId = "cadence_sensor";

    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    
    private BleAdvertiser? _advertiser;
    public static string? AdvertiserStatus { get; private set; }
    private BleCscServer? _gattServer;

    // Static state for the UI to peek at. Tiny app, single instance, this is fine.
    public static bool IsRunning { get; private set; }
    public static CadenceTracker? CurrentTracker { get; private set; }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopCapture();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (IsRunning)
            return StartCommandResult.Sticky;

        // Must call StartForeground within ~5s of OnStartCommand or Android kills us.
        var notification = Notifications.BuildPersistent(this, ChannelId, "Cadence sensor running");
        StartForeground(NotificationId, notification);

        StartCapture();
        IsRunning = true;
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        StopCapture();
        IsRunning = false;
        base.OnDestroy();
    }

    private void StartCapture()
    {
        _captureCts = new CancellationTokenSource();

        var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long Clock() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;

        var tracker = new CadenceTracker(Clock);
        CurrentTracker = tracker;

        var detector = new TickDetector(
            onTick: tracker.RecordTick,
            clock: Clock,
            options: new TickDetectorOptions());

        _captureTask = Task.Run(() => CaptureLoop(detector, _captureCts.Token));

        // BLE setup is async (we need to wait for GATT service registration to
        // complete before advertising). Fire and forget; errors surface via
        // AdvertiserStatus rather than throwing here.
        _ = Task.Run(async () =>
        {
            try
            {
                // GATT server first, awaited until BlueZ confirms the service is
                // registered. Otherwise the advertisement can attract a connection
                // before our GATT table is ready, and the watch sees an empty server.
                _gattServer = new BleCscServer(this, tracker);
                _gattServer.OnStatus += status => AdvertiserStatus = status;
                await _gattServer.StartAsync();

                _advertiser = new BleAdvertiser();
                _advertiser.OnStatus += status => AdvertiserStatus = status;
                _advertiser.Start();
            }
            catch (Exception ex)
            {
                AdvertiserStatus = $"BLE init failed: {ex.Message}";
            }
        });
    }

    private void StopCapture()
    {
        _advertiser?.Stop();
        _advertiser?.Dispose();
        _advertiser = null;

        _gattServer?.Stop();
        _gattServer?.Dispose();
        _gattServer = null;

        AdvertiserStatus = null;

        _captureCts?.Cancel();
        try { _captureTask?.Wait(2000); } catch { /* expected */ }
        _captureCts?.Dispose();
        _captureCts = null;
        _captureTask = null;
        CurrentTracker = null;
    }

    private static void CaptureLoop(TickDetector detector, CancellationToken ct)
    {
        using var source = new AndroidAudioSource(sampleRate: 44100, frameLength: 1024);
        source.Start();
        try
        {
            var frame = new short[source.FrameLength];
            while (!ct.IsCancellationRequested)
            {
                var read = source.ReadFrame(frame);
                if (read == 0) break;
                detector.ProcessFrame(frame.AsSpan(0, read));
            }
        }
        finally
        {
            source.Stop();
        }
    }
}