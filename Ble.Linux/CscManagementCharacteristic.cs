using Tmds.DBus;
using Core;

namespace Ble.Linux;

public sealed class CscMeasurementCharacteristic : IGattCharacteristic1
{
    public const string CscMeasurementUuid = "00002a5b-0000-1000-8000-00805f9b34fb";

    private readonly CadenceTracker _tracker;
    private readonly ObjectPath _servicePath;
    private byte[] _lastNotifiedValue = Array.Empty<byte>();
    private bool _notifying;

    public ObjectPath ObjectPath { get; }
    public event Action<PropertyChanges>? OnPropertiesChanged;

    public CscMeasurementCharacteristic(ObjectPath path, ObjectPath servicePath, CadenceTracker tracker)
    {
        ObjectPath = path;
        _servicePath = servicePath;
        _tracker = tracker;
    }

    public Task<byte[]> ReadValueAsync(IDictionary<string, object> options)
    {
        var snap = _tracker.Snapshot();
        return Task.FromResult(CscPacket.EncodeCrankRevolutions(
            snap.CumulativeCrankRevolutions, snap.LastCrankEventTime1024));
    }

    public Task WriteValueAsync(byte[] value, IDictionary<string, object> options) =>
        throw new NotSupportedException("CSC Measurement is notify-only.");

    public Task StartNotifyAsync()
    {
        _notifying = true;
        Console.WriteLine("[BLE] Notifications started");
        return Task.CompletedTask;
    }

    public Task StopNotifyAsync()
    {
        _notifying = false;
        Console.WriteLine("[BLE] Notifications stopped");
        return Task.CompletedTask;
    }

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);

    public Task<IDictionary<string, object>> GetAllAsync() =>
        Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            ["UUID"] = CscMeasurementUuid,
            ["Service"] = _servicePath,
            ["Flags"] = new[] { "notify" },
            ["Notifying"] = _notifying,
            ["Value"] = _lastNotifiedValue,
        });

    public Task<object> GetAsync(string name) => Task.FromResult<object>(name switch
    {
        "UUID" => CscMeasurementUuid,
        "Service" => _servicePath,
        "Flags" => new[] { "notify" },
        "Notifying" => _notifying,
        "Value" => _lastNotifiedValue,
        _ => throw new ArgumentException($"Unknown property {name}"),
    });

    /// <summary>
    /// Pushes the current tracker state as a notification, if anyone's subscribed.
    /// Returns true if a notification was sent.
    /// </summary>
    public bool TryNotify()
    {
        if (!_notifying) return false;

        var snap = _tracker.Snapshot();
        var packet = CscPacket.EncodeCrankRevolutions(snap.CumulativeCrankRevolutions, snap.LastCrankEventTime1024);
        _lastNotifiedValue = packet;

        // Invoking this event causes Tmds.DBus to emit the standard
        // org.freedesktop.DBus.Properties.PropertiesChanged signal, which is
        // exactly how BlueZ delivers GATT notifications to subscribed clients.
        OnPropertiesChanged?.Invoke(PropertyChanges.ForProperty("Value", packet));
        return true;
    }
}