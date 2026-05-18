using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.OS;
using Java.Util;

namespace Mobile;

/// <summary>
/// Wraps Android's BluetoothLeAdvertiser to broadcast the CSC service UUID
/// so the watch can discover us as a cycling sensor. No GATT yet — this is
/// purely the "I exist" announcement.
/// </summary>
public sealed class BleAdvertiser : IDisposable
{
    public const string CscServiceUuid = "00001816-0000-1000-8000-00805f9b34fb";

    private readonly BluetoothLeAdvertiser _advertiser;
    private readonly Callback _callback;
    private bool _advertising;

    public BleAdvertiser()
    {
        var manager =
            (BluetoothManager?)global::Android.App.Application.Context.GetSystemService((global::Android.Content.Context
                .BluetoothService))
            ?? throw new InvalidOperationException("Bluetooth service unavailable.");
        var adapter = manager.Adapter ?? throw new InvalidOperationException("Bluetooth adapter unavailable.");
        if (!adapter.IsEnabled)
            throw new InvalidOperationException("Bluetooth is turned off.");
        _advertiser = adapter.BluetoothLeAdvertiser
                      ?? throw new InvalidOperationException(
                          "This device does not support BLE peripheral advertising.");

        _callback = new Callback();
    }
    
    public event Action<string>? OnStatus;

    public void Start()
    {
        if (_advertising) return;

        _callback.OnStartSuccessAction = settings =>
            OnStatus?.Invoke($"Advertising started ({settings?.Mode}, txPower={settings?.TxPowerLevel})");
        _callback.OnStartFailureAction = code =>
            OnStatus?.Invoke($"Advertising failed: {code}");

        var settings = new AdvertiseSettings.Builder()!
                           .SetAdvertiseMode(AdvertiseMode.LowLatency)!
                           .SetTxPowerLevel(AdvertiseTx.PowerHigh)!
                           .SetConnectable(true)!
                           .Build()
                       ?? throw new InvalidOperationException("Failed to build advertise settings.");

        var data = new AdvertiseData.Builder()!
                       .SetIncludeDeviceName(true)!
                       .AddServiceUuid(ParcelUuid.FromString(CscServiceUuid))!
                       .Build()
                   ?? throw new InvalidOperationException("Failed to build advertise data.");

        _advertiser.StartAdvertising(settings, data, _callback);
        _advertising = true;
    }

    public void Stop()
    {
        if (!_advertising) return;
        _advertiser.StopAdvertising(_callback);
        _advertising = false;
    }

    public void Dispose() => Stop();

    private sealed class Callback : AdvertiseCallback
    {
        public Action<AdvertiseSettings?>? OnStartSuccessAction { get; set; }
        public Action<AdvertiseFailure>? OnStartFailureAction { get; set; }

        public override void OnStartSuccess(AdvertiseSettings? settingsInEffect) =>
            OnStartSuccessAction?.Invoke(settingsInEffect);

        public override void OnStartFailure(AdvertiseFailure errorCode) =>
            OnStartFailureAction?.Invoke(errorCode);
    }
}