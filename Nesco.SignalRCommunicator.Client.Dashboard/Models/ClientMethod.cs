namespace Nesco.SignalRCommunicator.Client.Dashboard.Models;

/// <summary>
/// Represents a registered client method for display in the dashboard
/// </summary>
public class ClientMethod
{
    public string Name { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
