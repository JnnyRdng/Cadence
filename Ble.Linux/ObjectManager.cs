using Tmds.DBus;

namespace Ble.Linux;

/// <summary>
/// Implements org.freedesktop.DBus.ObjectManager for our app root. BlueZ
/// calls GetManagedObjects on this object to discover the service tree.
///
/// Object path layout we expose:
///   /com/cadence            (this object, ObjectManager only)
///   /com/cadence/service0   (CscService)
///   /com/cadence/service0/char0   (CscMeasurementCharacteristic)
///   /com/cadence/service0/char1   (CscFeatureCharacteristic)
/// </summary>
public sealed class ObjectManager : IObjectManager
{
    private readonly CscService _service;
    private readonly CscMeasurementCharacteristic _measurement;
    private readonly CscFeatureCharacteristic _feature;

    public ObjectPath ObjectPath { get; }

    // These events are required by the IObjectManager contract but we never
    // emit them — our service tree is static once registered. The Watch...
    // methods exist purely to satisfy the interface.
    public event Action<PathInterfacesAndProperties>? OnInterfacesAdded;
    public event Action<PathAndInterfaces>? OnInterfacesRemoved;

    public ObjectManager(
        ObjectPath rootPath,
        CscService service,
        CscMeasurementCharacteristic measurement,
        CscFeatureCharacteristic feature)
    {
        ObjectPath = rootPath;
        _service = service;
        _measurement = measurement;
        _feature = feature;
    }

    public Task<IDisposable> WatchInterfacesAddedAsync(Action<PathInterfacesAndProperties> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnInterfacesAdded), handler);

    public Task<IDisposable> WatchInterfacesRemovedAsync(Action<PathAndInterfaces> handler) =>
        SignalWatcher.AddAsync(this, nameof(OnInterfacesRemoved), handler);

    public async Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync()
    {
        return new Dictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>
        {
            [_service.ObjectPath] = new Dictionary<string, IDictionary<string, object>>
            {
                ["org.bluez.GattService1"] = await _service.GetAllAsync(),
            },
            [_measurement.ObjectPath] = new Dictionary<string, IDictionary<string, object>>
            {
                ["org.bluez.GattCharacteristic1"] = await _measurement.GetAllAsync(),
            },
            [_feature.ObjectPath] = new Dictionary<string, IDictionary<string, object>>
            {
                ["org.bluez.GattCharacteristic1"] = await _feature.GetAllAsync(),
            },
        };
    }
}