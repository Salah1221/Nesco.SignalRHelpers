namespace Nesco.SignalRCommunicator.Dashboard.Models;

/// <summary>
/// Information about a single SignalR connection
/// </summary>
public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public string UserAgent { get; set; } = string.Empty;
}
