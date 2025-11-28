using Nesco.SignalRCommunicator.Client.Dashboard.Models;

namespace Nesco.SignalRCommunicator.Client.Dashboard.Services;

/// <summary>
/// Implementation of client method registry
/// </summary>
public class ClientMethodRegistry : IClientMethodRegistry
{
    private readonly List<ClientMethod> _methods = new();

    public void RegisterMethod(string name, string parameters, string returnType, string description, bool isDefault = false)
    {
        // Avoid duplicates
        if (_methods.Any(m => m.Name == name))
            return;

        _methods.Add(new ClientMethod
        {
            Name = name,
            Parameters = parameters,
            ReturnType = returnType,
            Description = description,
            IsDefault = isDefault
        });
    }

    public IEnumerable<ClientMethod> GetMethods()
    {
        return _methods.ToList();
    }

    public void Clear()
    {
        _methods.Clear();
    }
}
