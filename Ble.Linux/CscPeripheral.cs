using Tmds.DBus;
using Core;

namespace Ble.Linux;

public sealed class CscPeripheral : IAsyncDisposable
{
    private const string BluezBusName = "org.bluez";
    private const string AdapterPath = "/org/bluez/hci0";

    private readonly CadenceTracker _tracker;
    private readonly string _deviceName;
    private readonly TimeSpan _notifyInterval;

    private Connection? _connection;
    private CscMeasurementCharacteristic? _measurement;
    private CscAdvertisement? _advertisement;
    private CancellationTokenSource? _notifyLoopCts;
    private Task? _notifyLoopTask;

    public CscPeripheral(CadenceTracker tracker, string deviceName = "DIY-Bike-Cadence", TimeSpan? notifyInterval = null)
    {
        _tracker = tracker;
        _deviceName = deviceName;
        _notifyInterval = notifyInterval ?? TimeSpan.FromSeconds(1);
    }

    public async Task StartAsync()
    {
        // BlueZ lives on the system bus, not the session bus.
        _connection = new Connection(Address.System);
        await _connection.ConnectAsync();

        // Build the object tree.
        var rootPath = new ObjectPath("/com/cadence");
        var servicePath = new ObjectPath("/com/cadence/service0");
        var measurementPath = new ObjectPath("/com/cadence/service0/char0");
        var featurePath = new ObjectPath("/com/cadence/service0/char1");
        var advertPath = new ObjectPath("/com/cadence/advert0");

        var service = new CscService(servicePath);
        _measurement = new CscMeasurementCharacteristic(measurementPath, servicePath, _tracker);
        var feature = new CscFeatureCharacteristic(featurePath, servicePath);
        var manager = new ObjectManager(rootPath, service, _measurement, feature);
        _advertisement = new CscAdvertisement(advertPath, _deviceName);

        // Publish all objects to the bus. BlueZ will introspect them once we
        // tell it about the root via RegisterApplication.
        await _connection.RegisterObjectAsync(manager);
        await _connection.RegisterObjectAsync(service);
        await _connection.RegisterObjectAsync(_measurement);
        await _connection.RegisterObjectAsync(feature);
        await _connection.RegisterObjectAsync(_advertisement);

        // Tell BlueZ about our GATT application.
        var gattManager = _connection.CreateProxy<IGattManager1>(BluezBusName, AdapterPath);
        await gattManager.RegisterApplicationAsync(rootPath, new Dictionary<string, object>());
        Console.WriteLine("[BLE] GATT application registered");

        // Tell BlueZ to advertise.
        var advManager = _connection.CreateProxy<ILEAdvertisingManager1>(BluezBusName, AdapterPath);
        await advManager.RegisterAdvertisementAsync(advertPath, new Dictionary<string, object>());
        Console.WriteLine($"[BLE] Advertising as '{_deviceName}'");

        // Start the notification loop. Real cadence sensors push at 1Hz.
        _notifyLoopCts = new CancellationTokenSource();
        _notifyLoopTask = NotifyLoopAsync(_notifyLoopCts.Token);
    }

    private async Task NotifyLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_notifyInterval, ct);
                _measurement?.TryNotify();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BLE] Notify loop error: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_notifyLoopCts is not null)
        {
            _notifyLoopCts.Cancel();
            if (_notifyLoopTask is not null)
            {
                try { await _notifyLoopTask; } catch { /* expected */ }
            }
            _notifyLoopCts.Dispose();
        }

        if (_connection is not null && _advertisement is not null)
        {
            try
            {
                var advManager = _connection.CreateProxy<ILEAdvertisingManager1>(BluezBusName, AdapterPath);
                await advManager.UnregisterAdvertisementAsync(_advertisement.ObjectPath);
            }
            catch { /* may already be gone */ }
        }

        _connection?.Dispose();
    }
}