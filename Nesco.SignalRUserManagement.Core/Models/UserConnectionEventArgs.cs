namespace Nesco.SignalRUserManagement.Core.Models;

/// <summary>
/// Generic event args for user connection events
/// </summary>
public class UserConnectionEventArgs
{
    public string UserId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public UserConnectionEventType EventType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of connection event
/// </summary>
public enum UserConnectionEventType
{
    Connected,
    Disconnected,
    Reconnected
}
