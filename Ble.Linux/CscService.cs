using Tmds.DBus;

namespace Ble.Linux;

// The CSC service is a passive container. BlueZ asks for its properties via GetAll;
// we just return the static UUID and "Primary: true" flag.
public sealed class CscService : IGattService1
{
    public const string CscServiceUuid = "00001816-0000-1000-8000-00805f9b34fb";

    public ObjectPath ObjectPath { get; }

    public CscService(ObjectPath path)
    {
        ObjectPath = path;
    }

    public Task<IDictionary<string, object>> GetAllAsync() =>
        Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            ["UUID"] = CscServiceUuid,
            ["Primary"] = true,
        });
}