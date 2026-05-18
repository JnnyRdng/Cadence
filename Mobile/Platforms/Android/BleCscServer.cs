using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Core;
using Java.Util;
using Timer = System.Threading.Timer;

namespace Mobile;

/// <summary>
/// Hosts the BLE CSC (Cycling Speed and Cadence) GATT server. Once started,
/// any device that connects (typically the watch) can subscribe to the
/// Measurement characteristic and read the Feature characteristic to
/// discover that we report crank revolution data.
/// </summary>
public class BleCscServer : IDisposable
{
    private const string CscServiceUuid = "00001816-0000-1000-8000-00805f9b34fb";
    private const string CscMeasurementUuid = "00002a5b-0000-1000-8000-00805f9b34fb";
    private const string CscFeatureUuid = "00002a5c-0000-1000-8000-00805f9b34fb";

    // Standard Bluetooth SIG descriptor that clients write to to enable/disable
    // notifications. Subscription state is tracked via writes to this descriptor.
    private const string ClientCharConfigDescriptorUuid = "00002902-0000-1000-8000-00805f9b34fb";

    // CSC Feature value: bit 1 = "Crank Revolution Data Supported".
    private static readonly byte[] FeatureValue = { 0x02, 0x00 };

    private readonly Context _context;
    private readonly CadenceTracker _tracker;
    private readonly Callback _callback;
    private readonly HashSet<BluetoothDevice> _subscribers = new();
    private readonly object _subscribersLock = new();

    private BluetoothGattServer? _server;
    private BluetoothGattCharacteristic? _measurementChar;
    private Timer? _notifyTimer;

    public event Action<string>? OnStatus;

    public BleCscServer(Context context, CadenceTracker tracker)
    {
        _context = context;
        _tracker = tracker;
        _callback = new Callback(this);
    }

    public async Task StartAsync()
    {
        var manager = (BluetoothManager?)_context.GetSystemService(Context.BluetoothService)
                      ?? throw new InvalidOperationException("Bluetooth service unavailable.");

        _server = manager.OpenGattServer(_context, _callback)
                  ?? throw new InvalidOperationException("Failed to open GATT server.");

        // CSC service: Measurement (notify) + Feature (read).
        var cscService = new BluetoothGattService(
            UUID.FromString(CscServiceUuid)!,
            GattServiceType.Primary);

        // CSC Measurement: notify only. The CCC descriptor is how clients turn
        // notifications on; we add it explicitly because Android doesn't create
        // one automatically.
        _measurementChar = new BluetoothGattCharacteristic(
            UUID.FromString(CscMeasurementUuid)!,
            GattProperty.Notify,
            GattPermission.Read);

        var cccDescriptor = new BluetoothGattDescriptor(
            UUID.FromString(ClientCharConfigDescriptorUuid)!,
            GattDescriptorPermission.Read | GattDescriptorPermission.Write);
        _measurementChar.AddDescriptor(cccDescriptor);

        cscService.AddCharacteristic(_measurementChar);

        // CSC Feature: read only, static value.
        var featureChar = new BluetoothGattCharacteristic(
            UUID.FromString(CscFeatureUuid)!,
            GattProperty.Read,
            GattPermission.Read);
        featureChar.SetValue(FeatureValue);
        cscService.AddCharacteristic(featureChar);

        _server.AddService(cscService);
        await _callback.ServiceAdded.WaitAsync(TimeSpan.FromSeconds(5));

        // 1 Hz notification rate matches real cadence sensors.
        _notifyTimer = new Timer(_ => PushNotification(), null, 1000, 1000);

        OnStatus?.Invoke("GATT server started.");
    }

    private void PushNotification()
    {
        if (_server is null || _measurementChar is null) return;

        BluetoothDevice[] subs;
        lock (_subscribersLock)
        {
            if (_subscribers.Count == 0) return;
            subs = _subscribers.ToArray();
        }

        var snap = _tracker.Snapshot();
        var packet = CscPacket.EncodeCrankRevolutions(
            snap.CumulativeCrankRevolutions, snap.LastCrankEventTime1024);

        _measurementChar.SetValue(packet);

        foreach (var device in subs)
        {
            try
            {
                _server.NotifyCharacteristicChanged(device, _measurementChar, confirm: false);
            }
            catch (Exception ex)
            {
                OnStatus?.Invoke($"Notify failed for {device.Address}: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _notifyTimer?.Dispose();
        _notifyTimer = null;

        lock (_subscribersLock) _subscribers.Clear();

        _server?.Close();
        _server = null;
        _measurementChar = null;

        OnStatus?.Invoke("GATT server stopped.");
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Android's callback class for GATT server events. Forwards everything
    /// back to the outer BleCscServer so all the logic lives in one place.
    /// </summary>
    private sealed class Callback : BluetoothGattServerCallback
    {
        private readonly BleCscServer _outer;
        private readonly TaskCompletionSource _serviceAddedTcs = new();

        public Task ServiceAdded => _serviceAddedTcs.Task;

        public Callback(BleCscServer outer)
        {
            _outer = outer;
        }

        public override void OnServiceAdded(GattStatus status, BluetoothGattService? service)
        {
            _outer.OnStatus?.Invoke($"OnServiceAdded: {service?.Uuid} status={status}");
            if (status == GattStatus.Success)
                _serviceAddedTcs.TrySetResult();
            else
                _serviceAddedTcs.TrySetException(
                    new InvalidOperationException($"AddService failed: {status}"));
        }

        public override void OnConnectionStateChange(BluetoothDevice? device, ProfileState status,
            ProfileState newState)
        {
            if (device is null) return;
            _outer.OnStatus?.Invoke($"Conn {device.Address}: {newState}");

            if (newState == ProfileState.Disconnected)
            {
                lock (_outer._subscribersLock) _outer._subscribers.Remove(device);
            }
        }

        public override void OnCharacteristicReadRequest(
            BluetoothDevice? device, int requestId, int offset, BluetoothGattCharacteristic? characteristic)
        {
            if (device is null || characteristic is null || _outer._server is null) return;

            // We only have two read-capable characteristics: CSC Feature (static
            // value already SetValue'd) and CSC Measurement (which Android lets
            // clients read directly; we serve the current snapshot).
            byte[] value;
            if (characteristic.Uuid?.ToString().Equals(CscFeatureUuid, StringComparison.OrdinalIgnoreCase) == true)
            {
                value = FeatureValue;
            }
            else
            {
                var snap = _outer._tracker.Snapshot();
                value = CscPacket.EncodeCrankRevolutions(
                    snap.CumulativeCrankRevolutions, snap.LastCrankEventTime1024);
            }

            // Honour the offset that the client requested. For our short values
            // this is almost always 0.
            byte[] slice = offset >= value.Length
                ? Array.Empty<byte>()
                : value[offset..];

            _outer._server.SendResponse(device, requestId, GattStatus.Success, offset, slice);
        }

        public override void OnDescriptorReadRequest(
            BluetoothDevice? device, int requestId, int offset, BluetoothGattDescriptor? descriptor)
        {
            if (device is null || descriptor is null || _outer._server is null) return;

            // For the CCC descriptor, report whether this device is subscribed.
            bool subscribed;
            lock (_outer._subscribersLock) subscribed = _outer._subscribers.Contains(device);

            var value = subscribed
                ? new byte[] { 0x01, 0x00 } // notifications enabled
                : new byte[] { 0x00, 0x00 }; // disabled

            _outer._server.SendResponse(device, requestId, GattStatus.Success, offset, value);
        }

        public override void OnDescriptorWriteRequest(
            BluetoothDevice? device, int requestId, BluetoothGattDescriptor? descriptor,
            bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            if (device is null || descriptor is null || _outer._server is null) return;

            // The watch enables notifications by writing 0x0001 to the CCC
            // descriptor. 0x0000 disables. (Indications would be 0x0002 but
            // we declared notify-only.)
            bool subscribe = value is { Length: >= 1 } && value[0] == 0x01;

            lock (_outer._subscribersLock)
            {
                if (subscribe)
                    _outer._subscribers.Add(device);
                else
                    _outer._subscribers.Remove(device);
            }

            _outer.OnStatus?.Invoke(
                subscribe
                    ? $"{device.Address} subscribed."
                    : $"{device.Address} unsubscribed.");

            if (responseNeeded)
                _outer._server.SendResponse(device, requestId, GattStatus.Success, offset, value);
        }
    }
}