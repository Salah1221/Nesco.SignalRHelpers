namespace Nesco.SignalRUserManagement.Core.Models;

/// <summary>
/// Represents a single SignalR connection
/// </summary>
public class ConnectionDTO
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool Connected { get; set; }
    public DateTime ConnectedAt { get; set; }
}
