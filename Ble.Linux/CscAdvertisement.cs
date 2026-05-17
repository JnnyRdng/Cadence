using Tmds.DBus;

namespace Ble.Linux;

public sealed class CscAdvertisement : ILEAdvertisement1
{
    private readonly string _localName;

    public ObjectPath ObjectPath { get; }

    public CscAdvertisement(ObjectPath path, string localName)
    {
        ObjectPath = path;
        _localName = localName;
    }

    /// <summary>
    /// BlueZ calls Release when it removes our advertisement (e.g. on shutdown
    /// or when another advertisement takes our slot). Nothing to do here.
    /// </summary>
    public Task ReleaseAsync()
    {
        Console.WriteLine("[BLE] Advertisement released by BlueZ");
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> GetAllAsync() =>
        Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            ["Type"] = "peripheral",
            ["ServiceUUIDs"] = new[] { CscService.CscServiceUuid },
            ["LocalName"] = _localName,
            // Generic Cycling appearance: 0x0480. Not strictly required.
            ["Appearance"] = (ushort)0x0480,
            // Don't include TxPower; BlueZ on some systems errors if it's set
            // without proper support.
        });
}