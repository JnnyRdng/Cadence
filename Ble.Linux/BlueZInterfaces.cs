using Tmds.DBus;

namespace Ble.Linux;

[DBusInterface("org.bluez.GattManager1")]
public interface IGattManager1 : IDBusObject
{
    Task RegisterApplicationAsync(ObjectPath application, IDictionary<string, object> options);
    Task UnregisterApplicationAsync(ObjectPath application);
}

[DBusInterface("org.bluez.LEAdvertisingManager1")]
public interface ILEAdvertisingManager1 : IDBusObject
{
    Task RegisterAdvertisementAsync(ObjectPath advertisement, IDictionary<string, object> options);
    Task UnregisterAdvertisementAsync(ObjectPath advertisement);
}