namespace Nesco.SignalRUserManagement.Core.Interfaces;

/// <summary>
/// Service for managing SignalR user connections and sending messages
/// </summary>
public interface IUserConnectionService
{
    /// <summary>
    /// Sends a message to all connected clients
    /// </summary>
    Task SendToAllAsync(string method, object? data = null);

    /// <summary>
    /// Sends a message to all connections for a specific user
    /// </summary>
    Task SendToUserAsync(string userId, string method, object? data = null);

    /// <summary>
    /// Sends a message to a specific connection
    /// </summary>
    Task SendToConnectionAsync(string connectionId, string method, object? data = null);

    /// <summary>
    /// Sends a message to all connections for multiple users
    /// </summary>
    Task SendToUsersAsync(IEnumerable<string> userIds, string method, object? data = null);

    /// <summary>
    /// Gets the number of currently connected users
    /// </summary>
    int GetConnectedUsersCount();

    /// <summary>
    /// Gets the number of active connections
    /// </summary>
    int GetActiveConnectionsCount();

    /// <summary>
    /// Checks if a specific user has any active connections
    /// </summary>
    bool IsUserConnected(string userId);
}
