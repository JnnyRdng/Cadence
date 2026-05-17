using Tmds.DBus;

namespace Ble.Linux;

// BlueZ-defined interface IDs. We implement these on our objects.
[DBusInterface("org.bluez.GattService1")]
public interface IGattService1 : IDBusObject
{
    Task<IDictionary<string, object>> GetAllAsync();
}

[DBusInterface("org.bluez.GattCharacteristic1")]
public interface IGattCharacteristic1 : IDBusObject
{
    Task<byte[]> ReadValueAsync(IDictionary<string, object> options);
    Task WriteValueAsync(byte[] value, IDictionary<string, object> options);
    Task StartNotifyAsync();
    Task StopNotifyAsync();
    Task<IDictionary<string, object>> GetAllAsync();
    Task<object> GetAsync(string name);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[DBusInterface("org.bluez.LEAdvertisement1")]
public interface ILEAdvertisement1 : IDBusObject
{
    Task ReleaseAsync();
    Task<IDictionary<string, object>> GetAllAsync();
}

// org.freedesktop.DBus.ObjectManager is how BlueZ discovers our objects.
[DBusInterface("org.freedesktop.DBus.ObjectManager")]
public interface IObjectManager : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
    Task<IDisposable> WatchInterfacesAddedAsync(Action<PathInterfacesAndProperties> handler);
    Task<IDisposable> WatchInterfacesRemovedAsync(Action<PathAndInterfaces> handler);
}

[DBusInterface("org.freedesktop.DBus.Properties")]
public interface IPropertiesChanged : IDBusObject
{
    Task<object> GetAsync(string @interface, string prop);
    Task<IDictionary<string, object>> GetAllAsync(string @interface);
    Task SetAsync(string @interface, string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

public record struct PathInterfacesAndProperties(ObjectPath Path, IDictionary<string, IDictionary<string, object>> Interfaces);
public record struct PathAndInterfaces(ObjectPath Path, string[] Interfaces);