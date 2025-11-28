using Microsoft.AspNetCore.SignalR;

namespace Nesco.SignalRUserManagement.Core.Options;

/// <summary>
/// Configuration options for SignalR User Management
/// </summary>
public class UserManagementOptions
{
    /// <summary>
    /// Whether to broadcast connection events to all clients
    /// Default: true
    /// </summary>
    public bool BroadcastConnectionEvents { get; set; } = true;

    /// <summary>
    /// Method name for connection event broadcasts
    /// Default: "UserConnectionEvent"
    /// </summary>
    public string ConnectionEventMethod { get; set; } = "UserConnectionEvent";

    /// <summary>
    /// Whether to automatically purge offline connections on connect
    /// Default: true
    /// </summary>
    public bool AutoPurgeOfflineConnections { get; set; } = true;

    /// <summary>
    /// Keep-alive interval in seconds for SignalR connections
    /// Default: 15
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Client timeout in seconds
    /// Default: 30
    /// </summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to track user agent information
    /// Default: true
    /// </summary>
    public bool TrackUserAgent { get; set; } = true;

    /// <summary>
    /// Whether to automatically reconnect when connection drops
    /// Default: true
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Retry delays in seconds for automatic reconnection attempts
    /// Default: [0, 2, 5, 10, 30] - Progressive backoff
    /// </summary>
    public int[] AutoReconnectRetryDelaysSeconds { get; set; } = new[] { 0, 2, 5, 10, 30 };

    /// <summary>
    /// Optional callback invoked after a user successfully connects
    /// Parameters: (IHubCallerClients clients, string userId, string connectionId, HubCallerContext context)
    /// </summary>
    public Func<IHubCallerClients, string, string, HubCallerContext, Task>? OnUserConnected { get; set; }

    /// <summary>
    /// Optional callback invoked before a user disconnects
    /// Parameters: (IHubCallerClients clients, string userId, string connectionId, HubCallerContext context)
    /// </summary>
    public Func<IHubCallerClients, string, string, HubCallerContext, Task>? OnUserDisconnected { get; set; }
}
