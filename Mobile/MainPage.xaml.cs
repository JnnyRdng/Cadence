using Android.Content;

namespace Mobile;

public partial class MainPage : ContentPage
{
    private IDispatcherTimer? _uiTimer;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // UI timer runs whenever the page is visible. Stops when navigating away
        // or when the app is backgrounded, but the service keeps capturing.
        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(200);
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();
        RefreshUi();
    }

    protected override void OnDisappearing()
    {
        _uiTimer?.Stop();
        _uiTimer = null;
        base.OnDisappearing();
    }

    private void RefreshUi()
    {
        if (CadenceService.IsRunning)
        {
            ToggleButton.Text = "Stop";
            var rpm = CadenceService.CurrentTracker?.CurrentRpm ?? 0;
            RpmLabel.Text = $"{rpm:F0}";
            StatusLabel.Text = CadenceService.AdvertiserStatus ?? "Running...";
        }
        else
        {
            ToggleButton.Text = "Start";
            StatusLabel.Text = "Press Start to begin.";
            RpmLabel.Text = "—";
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (CadenceService.IsRunning)
        {
            StopService();
        }
        else
        {
            await StartServiceAsync();
        }
        RefreshUi();
    }

    private async Task StartServiceAsync()
    {
        var micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
        if (micStatus != PermissionStatus.Granted)
        {
            StatusLabel.Text = "Microphone permission denied.";
            return;
        }

        // BLE advertising on Android 12+ requires Bluetooth runtime permissions.
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var bleStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
            if (bleStatus != PermissionStatus.Granted)
            {
                StatusLabel.Text = "Bluetooth permission denied.";
                return;
            }
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
            // Continue regardless — service runs without notification permission.
        }

        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(CadenceService));
        intent.SetAction(CadenceService.ActionStart);
        context.StartForegroundService(intent);
    }

    private static void StopService()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(CadenceService));
        intent.SetAction(CadenceService.ActionStop);
        context.StartService(intent);
    }
}