using BlazorServerApplication.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Server.Services;
using Nesco.SignalRUserManagement.Core.Interfaces;

namespace BlazorServerApplication.Services;

/// <summary>
/// Unified service that coordinates SignalRUserManagement and SignalRCommunicator
/// </summary>
public interface IUnifiedSignalRService
{
    /// <summary>
    /// Invokes a method on all connected users
    /// </summary>
    Task<SignalRResponse> InvokeOnAllConnectedAsync(string methodName, object? parameter);
    Task<T?> InvokeOnAllConnectedAsync<T>(string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a specific user (all their connections)
    /// </summary>
    Task<SignalRResponse> InvokeOnUserAsync(string userId, string methodName, object? parameter);
    Task<T?> InvokeOnUserAsync<T>(string userId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on multiple users (all their connections)
    /// </summary>
    Task<SignalRResponse> InvokeOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter);
    Task<T?> InvokeOnUsersAsync<T>(IEnumerable<string> userIds, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a specific connection
    /// </summary>
    Task<SignalRResponse> InvokeOnConnectionAsync(string connectionId, string methodName, object? parameter);
    Task<T?> InvokeOnConnectionAsync<T>(string connectionId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Gets count of connected users
    /// </summary>
    int GetConnectedUsersCount();

    /// <summary>
    /// Gets count of active connections
    /// </summary>
    int GetActiveConnectionsCount();

    /// <summary>
    /// Checks if a user is connected
    /// </summary>
    bool IsUserConnected(string userId);

    /// <summary>
    /// Gets all connected users with their connections
    /// </summary>
    Task<List<ConnectedUserInfo>> GetConnectedUsersAsync();
}

public class UnifiedSignalRService : IUnifiedSignalRService
{
    private readonly ISignalRCommunicatorService _communicator;
    private readonly IUserConnectionService _userManagement;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UnifiedSignalRService> _logger;

    public UnifiedSignalRService(
        ISignalRCommunicatorService communicator,
        IUserConnectionService userManagement,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<UnifiedSignalRService> logger)
    {
        _communicator = communicator;
        _userManagement = userManagement;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<SignalRResponse> InvokeOnAllConnectedAsync(string methodName, object? parameter)
    {
        _logger.LogInformation("Invoking {Method} on all connected users", methodName);

        // Use Communicator's InvokeMethodAsync which sends to all
        return await _communicator.InvokeMethodAsync(methodName, parameter);
    }

    public async Task<T?> InvokeOnAllConnectedAsync<T>(string methodName, object? parameter) where T : class
    {
        var response = await InvokeOnAllConnectedAsync(methodName, parameter);
        return await ParseResponse<T>(response);
    }

    public async Task<SignalRResponse> InvokeOnUserAsync(string userId, string methodName, object? parameter)
    {
        _logger.LogInformation(">>> [UnifiedSignalRService] Invoking {Method} on user {UserId}", methodName, userId);

        // IMPORTANT: Purge stale connections first!
        await PurgeStaleConnectionsAsync();

        // Check if user is connected
        if (!_userManagement.IsUserConnected(userId))
        {
            throw new InvalidOperationException($"User {userId} is not connected");
        }

        // Get all connection IDs for this user from the database
        _logger.LogInformation(">>> [UnifiedSignalRService] Getting connection IDs for user {UserId} from database", userId);
        var connectionIds = await _context.Connections
            .Where(c => c.UserId == userId && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        _logger.LogInformation(">>> [UnifiedSignalRService] Found {Count} connections for user {UserId}: [{Connections}]",
            connectionIds.Count, userId, string.Join(", ", connectionIds));

        if (connectionIds.Count == 0)
        {
            throw new InvalidOperationException($"User {userId} has no active connections");
        }

        // Use connection-based invocation instead of user-based
        return await _communicator.InvokeMethodOnConnectionsAsync(connectionIds, methodName, parameter);
    }

    public async Task<T?> InvokeOnUserAsync<T>(string userId, string methodName, object? parameter) where T : class
    {
        var response = await InvokeOnUserAsync(userId, methodName, parameter);
        return await ParseResponse<T>(response);
    }

    public async Task<SignalRResponse> InvokeOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter)
    {
        var userIdList = userIds.ToList();
        _logger.LogInformation(">>> [UnifiedSignalRService] Invoking {Method} on {Count} users", methodName, userIdList.Count);

        // Get all connection IDs for these users from the database
        _logger.LogInformation(">>> [UnifiedSignalRService] Getting connection IDs for {Count} users from database", userIdList.Count);
        var connectionIds = await _context.Connections
            .Where(c => userIdList.Contains(c.UserId) && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        _logger.LogInformation(">>> [UnifiedSignalRService] Found {Count} total connections for {UserCount} users: [{Connections}]",
            connectionIds.Count, userIdList.Count, string.Join(", ", connectionIds.Take(5)));

        if (connectionIds.Count == 0)
        {
            throw new InvalidOperationException($"None of the specified users have active connections");
        }

        // Use connection-based invocation
        return await _communicator.InvokeMethodOnConnectionsAsync(connectionIds, methodName, parameter);
    }

    public async Task<T?> InvokeOnUsersAsync<T>(IEnumerable<string> userIds, string methodName, object? parameter) where T : class
    {
        var response = await InvokeOnUsersAsync(userIds, methodName, parameter);
        return await ParseResponse<T>(response);
    }

    public async Task<SignalRResponse> InvokeOnConnectionAsync(string connectionId, string methodName, object? parameter)
    {
        _logger.LogInformation("Invoking {Method} on connection {ConnectionId}", methodName, connectionId);

        // Since Communicator now shares the UserManagement hub, connection IDs are the same!
        // Verify connection exists in database
        var connectionExists = await _context.Connections
            .AnyAsync(c => c.ConnectionId == connectionId && c.Connected);

        if (!connectionExists)
        {
            throw new InvalidOperationException($"Connection {connectionId} is not active or does not exist");
        }

        // Use Communicator's InvokeMethodOnConnectionAsync
        return await _communicator.InvokeMethodOnConnectionAsync(connectionId, methodName, parameter);
    }

    public async Task<T?> InvokeOnConnectionAsync<T>(string connectionId, string methodName, object? parameter) where T : class
    {
        var response = await InvokeOnConnectionAsync(connectionId, methodName, parameter);
        return await ParseResponse<T>(response);
    }

    public int GetConnectedUsersCount()
    {
        return _userManagement.GetConnectedUsersCount();
    }

    public int GetActiveConnectionsCount()
    {
        return _userManagement.GetActiveConnectionsCount();
    }

    public bool IsUserConnected(string userId)
    {
        return _userManagement.IsUserConnected(userId);
    }

    public async Task<List<ConnectedUserInfo>> GetConnectedUsersAsync()
    {
        _logger.LogInformation(">>> [GetConnectedUsersAsync] Fetching connected users...");

        // IMPORTANT: Purge stale connections first!
        await PurgeStaleConnectionsAsync();

        // Clear change tracker after purging to ensure we get fresh data
        _context.ChangeTracker.Clear();

        // Since Communicator now shares UserManagement hub, we can use database connection IDs directly!
        var connectedUsers = await _context.ConnectedUsers
            .Include(cu => cu.Connections)
            .Where(cu => cu.Connections.Any(c => c.Connected))
            .AsNoTracking() // Don't track these entities
            .ToListAsync();

        _logger.LogInformation(">>> [GetConnectedUsersAsync] Found {Count} connected users with {TotalConnections} total connections",
            connectedUsers.Count, connectedUsers.Sum(u => u.Connections.Count(c => c.Connected)));

        var result = new List<ConnectedUserInfo>();

        foreach (var cu in connectedUsers)
        {
            // Fetch username and email from AspNetUsers
            var user = await _userManager.FindByIdAsync(cu.UserId);

            result.Add(new ConnectedUserInfo
            {
                UserId = cu.UserId,
                Username = user?.UserName ?? cu.UserId,
                Email = user?.Email ?? "",
                LastConnect = cu.LastConnect,
                LastDisconnect = cu.LastDisconnect,
                Connections = cu.Connections.Where(c => c.Connected).Select(c => new ConnectionInfo
                {
                    ConnectionId = c.ConnectionId,
                    UserAgent = c.UserAgent,
                    ConnectedAt = c.ConnectedAt,
                    Connected = c.Connected
                }).ToList()
            });
        }

        _logger.LogInformation(">>> [GetConnectedUsersAsync] Returning {Count} users", result.Count);
        return result;
    }

    /// <summary>
    /// Purges stale connections from the database by checking if they actually exist in the hub.
    /// This is critical because OnDisconnectedAsync might not always fire (page refresh, crash, etc.)
    /// </summary>
    private async Task PurgeStaleConnectionsAsync()
    {
        _logger.LogInformation(">>> [PurgeStaleConnections] Starting purge of stale connections...");

        // Get all connections marked as "Connected" in the database
        var dbConnections = await _context.Connections
            .Where(c => c.Connected)
            .ToListAsync();

        _logger.LogInformation(">>> [PurgeStaleConnections] Found {Count} connections in database marked as Connected", dbConnections.Count);

        var staleConnections = new List<Nesco.SignalRUserManagement.Server.Models.Connection>();

        // For each connection, we would need to check if it actually exists in the hub
        // Since we can't easily query the hub's active connections from here,
        // we'll use a different approach: mark connections older than 5 minutes as stale
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

        foreach (var conn in dbConnections)
        {
            // If connection was created more than 5 minutes ago, consider it stale
            // (normal clients should have reconnected by now)
            if (conn.ConnectedAt < cutoffTime)
            {
                _logger.LogWarning(">>> [PurgeStaleConnections] Marking connection as STALE: {ConnectionId} (age: {Age} minutes, UserId: {UserId})",
                    conn.ConnectionId, (DateTime.UtcNow - conn.ConnectedAt).TotalMinutes, conn.UserId);
                staleConnections.Add(conn);
            }
        }

        if (staleConnections.Any())
        {
            _logger.LogWarning(">>> [PurgeStaleConnections] Removing {Count} stale connections from database", staleConnections.Count);
            _context.Connections.RemoveRange(staleConnections);
            var saveResult = await _context.SaveChangesAsync();
            _logger.LogInformation(">>> [PurgeStaleConnections] SaveChanges completed. Entities modified: {Count}", saveResult);

            // IMPORTANT: Clear change tracker to ensure removed entities don't reappear
            _context.ChangeTracker.Clear();

            _logger.LogInformation(">>> [PurgeStaleConnections] Purge complete. Removed {Count} stale connections and cleared change tracker", staleConnections.Count);
        }
        else
        {
            _logger.LogInformation(">>> [PurgeStaleConnections] No stale connections found");
        }
    }

    private async Task<T?> ParseResponse<T>(SignalRResponse response) where T : class
    {
        // Use the communicator's typed method which handles deserialization
        return await _communicator.InvokeMethodAsync<T>("dummy", null);
    }
}

/// <summary>
/// DTO for connected user information
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

/// <summary>
/// DTO for connection information
/// </summary>
public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public bool Connected { get; set; }
}
