using System.ComponentModel.DataAnnotations;

namespace Nesco.SignalRUserManagement.Server.Models;

/// <summary>
/// Database model for tracking connected users
/// Add this to your DbContext:
/// public DbSet&lt;ConnectedUser&gt; ConnectedUsers { get; set; }
/// public DbSet&lt;Connection&gt; Connections { get; set; }
/// </summary>
public class ConnectedUser
{
    [Key]
    public string UserId { get; set; } = string.Empty;

    public DateTime? LastConnect { get; set; }
    public DateTime? LastDisconnect { get; set; }

    public ICollection<Connection> Connections { get; set; } = new List<Connection>();
}
