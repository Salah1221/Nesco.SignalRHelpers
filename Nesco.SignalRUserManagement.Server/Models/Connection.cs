using System.ComponentModel.DataAnnotations;

namespace Nesco.SignalRUserManagement.Server.Models;

/// <summary>
/// Database model for individual SignalR connections
/// </summary>
public class Connection
{
    [Key]
    public string ConnectionId { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;
    public bool Connected { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    // Foreign key to ConnectedUser
    public string UserId { get; set; } = string.Empty;
    public ConnectedUser? User { get; set; }
}
