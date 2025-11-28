using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Dashboard.Models;
using Nesco.SignalRCommunicator.Server.Services;
using Nesco.SignalRUserManagement.Core.Interfaces;
using Nesco.SignalRUserManagement.Server.Models;

namespace Nesco.SignalRCommunicator.Dashboard.Services;

/// <summary>
/// Implementation of the dashboard service that coordinates SignalR operations
/// </summary>
/// <typeparam name="TUser">The IdentityUser type</typeparam>
/// <typeparam name="TDbContext">The DbContext type that contains Connections and ConnectedUsers DbSets</typeparam>
public class DashboardService<TUser, TDbContext> : IDashboardService
    where TUser : IdentityUser
    where TDbContext : DbContext
{
    private readonly ISignalRCommunicatorService _communicator;
    private readonly IUserConnectionService _userManagement;
    private readonly TDbContext _context;
    private readonly UserManager<TUser> _userManager;
    private readonly ILogger<DashboardService<TUser, TDbContext>> _logger;

    public DashboardService(
        ISignalRCommunicatorService communicator,
        IUserConnectionService userManagement,
        TDbContext context,
        UserManager<TUser> userManager,
        ILogger<DashboardService<TUser, TDbContext>> logger)
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
        return await _communicator.InvokeMethodAsync(methodName, parameter);
    }

    public async Task<T?> InvokeOnAllConnectedAsync<T>(string methodName, object? parameter) where T : class
    {
        var response = await InvokeOnAllConnectedAsync(methodName, parameter);
        return await ParseResponse<T>(response);
    }

    public async Task<SignalRResponse> InvokeOnUserAsync(string userId, string methodName, object? parameter)
    {
        _logger.LogInformation("Invoking {Method} on user {UserId}", methodName, userId);

        // Purge stale connections first
        await PurgeStaleConnectionsAsync();

        // Check if user is connected
        if (!_userManagement.IsUserConnected(userId))
        {
            throw new InvalidOperationException($"User {userId} is not connected");
        }

        // Get all connection IDs for this user from the database
        var connectionIds = await _context.Set<Connection>()
            .Where(c => c.UserId == userId && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        _logger.LogInformation("Found {Count} connections for user {UserId}", connectionIds.Count, userId);

        if (connectionIds.Count == 0)
        {
            throw new InvalidOperationException($"User {userId} has no active connections");
        }

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
        _logger.LogInformation("Invoking {Method} on {Count} users", methodName, userIdList.Count);

        // Get all connection IDs for these users from the database
        var connectionIds = await _context.Set<Connection>()
            .Where(c => userIdList.Contains(c.UserId) && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        _logger.LogInformation("Found {Count} total connections for {UserCount} users", connectionIds.Count, userIdList.Count);

        if (connectionIds.Count == 0)
        {
            throw new InvalidOperationException($"None of the specified users have active connections");
        }

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

        // Verify connection exists in database
        var connectionExists = await _context.Set<Connection>()
            .AnyAsync(c => c.ConnectionId == connectionId && c.Connected);

        if (!connectionExists)
        {
            throw new InvalidOperationException($"Connection {connectionId} is not active or does not exist");
        }

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
        _logger.LogInformation("Fetching connected users...");

        // Purge stale connections first
        await PurgeStaleConnectionsAsync();

        // Clear change tracker after purging
        _context.ChangeTracker.Clear();

        // Get connected users with their connections
        var connectedUsers = await _context.Set<ConnectedUser>()
            .Include(cu => cu.Connections)
            .Where(cu => cu.Connections.Any(c => c.Connected))
            .AsNoTracking()
            .ToListAsync();

        _logger.LogInformation("Found {Count} connected users with {TotalConnections} total connections",
            connectedUsers.Count, connectedUsers.Sum(u => u.Connections.Count(c => c.Connected)));

        var result = new List<ConnectedUserInfo>();

        foreach (var cu in connectedUsers)
        {
            // Fetch username and email from Identity
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
                    ConnectedAt = c.ConnectedAt
                }).ToList()
            });
        }

        _logger.LogInformation("Returning {Count} users", result.Count);
        return result;
    }

    /// <summary>
    /// Purges stale connections from the database
    /// </summary>
    private async Task PurgeStaleConnectionsAsync()
    {
        _logger.LogInformation("Starting purge of stale connections...");

        // Get all connections marked as "Connected" in the database
        var dbConnections = await _context.Set<Connection>()
            .Where(c => c.Connected)
            .ToListAsync();

        _logger.LogInformation("Found {Count} connections in database marked as Connected", dbConnections.Count);

        var staleConnections = new List<Connection>();

        // Mark connections older than 5 minutes as stale
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

        foreach (var conn in dbConnections)
        {
            if (conn.ConnectedAt < cutoffTime)
            {
                _logger.LogWarning("Marking connection as STALE: {ConnectionId} (age: {Age} minutes, UserId: {UserId})",
                    conn.ConnectionId, (DateTime.UtcNow - conn.ConnectedAt).TotalMinutes, conn.UserId);
                staleConnections.Add(conn);
            }
        }

        if (staleConnections.Any())
        {
            _logger.LogWarning("Removing {Count} stale connections from database", staleConnections.Count);
            _context.Set<Connection>().RemoveRange(staleConnections);
            var saveResult = await _context.SaveChangesAsync();
            _logger.LogInformation("SaveChanges completed. Entities modified: {Count}", saveResult);

            // Clear change tracker
            _context.ChangeTracker.Clear();

            _logger.LogInformation("Purge complete. Removed {Count} stale connections", staleConnections.Count);
        }
        else
        {
            _logger.LogInformation("No stale connections found");
        }
    }

    private async Task<T?> ParseResponse<T>(SignalRResponse response) where T : class
    {
        // Use the communicator's typed method which handles deserialization
        return await _communicator.InvokeMethodAsync<T>("dummy", null);
    }
}
