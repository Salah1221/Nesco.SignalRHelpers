namespace Nesco.SignalRUserManagement.Core.Models;

/// <summary>
/// Represents a connected user with their active connections
/// </summary>
public class ConnectedUserDTO
{
    public string UserId { get; set; } = string.Empty;
    public DateTime? LastConnect { get; set; }
    public DateTime? LastDisconnect { get; set; }
    public List<ConnectionDTO> Connections { get; set; } = new();
    public int NumberOfConnections => Connections?.Count ?? 0;
}
