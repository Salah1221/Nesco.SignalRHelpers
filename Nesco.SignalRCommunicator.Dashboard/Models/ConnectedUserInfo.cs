namespace Nesco.SignalRCommunicator.Dashboard.Models;

/// <summary>
/// Information about a connected user and their connections
/// </summary>
public class ConnectedUserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? LastConnect { get; set; }
    public DateTime? LastDisconnect { get; set; }
    public List<ConnectionInfo> Connections { get; set; } = new();
}
