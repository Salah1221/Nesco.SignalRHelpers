using Nesco.SignalRCommunicator.Client.Dashboard.Models;

namespace Nesco.SignalRCommunicator.Client.Dashboard.Services;

/// <summary>
/// Registry for client methods that can be invoked by the server
/// </summary>
public interface IClientMethodRegistry
{
    /// <summary>
    /// Registers a client method for display in the dashboard
    /// </summary>
    void RegisterMethod(string name, string parameters, string returnType, string description, bool isDefault = false);

    /// <summary>
    /// Gets all registered methods
    /// </summary>
    IEnumerable<ClientMethod> GetMethods();

    /// <summary>
    /// Clears all registered methods
    /// </summary>
    void Clear();
}
