using System.Buffers.Binary;
using Tmds.DBus;

namespace Ble.Linux;

public sealed class CscFeatureCharacteristic : IGattCharacteristic1
{
    public const string CscFeatureUuid = "00002a5c-0000-1000-8000-00805f9b34fb";

    // Bit 1 = crank revolution data supported. Bit 0 (wheel) is off.
    private static readonly byte[] FeatureValue = MakeFeatureValue();

    private readonly ObjectPath _servicePath;

    public ObjectPath ObjectPath { get; }
    public event Action<PropertyChanges>? OnPropertiesChanged;

    public CscFeatureCharacteristic(ObjectPath path, ObjectPath servicePath)
    {
        ObjectPath = path;
        _servicePath = servicePath;
    }

    private static byte[] MakeFeatureValue()
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 0x0002);
        return buf;
    }

    public Task<byte[]> ReadValueAsync(IDictionary<string, object> options) =>
        Task.FromResult(FeatureValue);

    public Task WriteValueAsync(byte[] value, IDictionary<string, object> options) =>
        throw new NotSupportedException("CSC Feature is read-only.");

    public Task StartNotifyAsync() => throw new NotSupportedException();
    public Task StopNotifyAsync() => throw new NotSupportedException();

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnPropertiesChanged), handler);

    public Task<IDictionary<string, object>> GetAllAsync() =>
        Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            ["UUID"] = CscFeatureUuid,
            ["Service"] = _servicePath,
            ["Flags"] = new[] { "read" },
            ["Value"] = FeatureValue,
        });

    public Task<object> GetAsync(string name) => Task.FromResult<object>(name switch
    {
        "UUID" => CscFeatureUuid,
        "Service" => _servicePath,
        "Flags" => new[] { "read" },
        "Value" => FeatureValue,
        _ => throw new ArgumentException($"Unknown property {name}"),
    });
}