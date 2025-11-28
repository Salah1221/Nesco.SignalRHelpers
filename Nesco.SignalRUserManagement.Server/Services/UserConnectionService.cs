using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nesco.SignalRUserManagement.Core.Interfaces;
using Nesco.SignalRUserManagement.Server.Models;

namespace Nesco.SignalRUserManagement.Server.Services;

/// <summary>
/// Service for managing user connections and sending messages to specific users/connections
/// </summary>
/// <typeparam name="THub">The SignalR hub type</typeparam>
/// <typeparam name="TDbContext">The DbContext containing user connection data</typeparam>
public class UserConnectionService<THub, TDbContext> : IUserConnectionService
    where THub : Hub
    where TDbContext : DbContext
{
    private readonly IHubContext<THub> _hubContext;
    private readonly TDbContext _context;
    private readonly ILogger<UserConnectionService<THub, TDbContext>> _logger;

    public UserConnectionService(
        IHubContext<THub> hubContext,
        TDbContext context,
        ILogger<UserConnectionService<THub, TDbContext>> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task SendToAllAsync(string method, object? data = null)
    {
        _logger.LogDebug("Sending '{Method}' to all clients", method);
        await _hubContext.Clients.All.SendAsync(method, data);
    }

    /// <inheritdoc/>
    public async Task SendToUserAsync(string userId, string method, object? data = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
        }

        _logger.LogDebug("Sending '{Method}' to user {UserId}", method, userId);

        // Get all active connection IDs for this user
        var connectionIds = await GetConnectionsDbSet()
            .Where(c => c.UserId == userId && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        if (!connectionIds.Any())
        {
            _logger.LogWarning("No active connections found for user {UserId}", userId);
            return;
        }

        // Send to all connections for this user
        await _hubContext.Clients.Clients(connectionIds).SendAsync(method, data);
        _logger.LogDebug("Sent '{Method}' to {Count} connections for user {UserId}", method, connectionIds.Count, userId);
    }

    /// <inheritdoc/>
    public async Task SendToConnectionAsync(string connectionId, string method, object? data = null)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException("ConnectionId cannot be null or empty", nameof(connectionId));
        }

        _logger.LogDebug("Sending '{Method}' to connection {ConnectionId}", method, connectionId);
        await _hubContext.Clients.Client(connectionId).SendAsync(method, data);
    }

    /// <inheritdoc/>
    public async Task SendToUsersAsync(IEnumerable<string> userIds, string method, object? data = null)
    {
        var userIdList = userIds?.ToList();
        if (userIdList == null || !userIdList.Any())
        {
            throw new ArgumentException("UserIds cannot be null or empty", nameof(userIds));
        }

        _logger.LogDebug("Sending '{Method}' to {Count} users", method, userIdList.Count);

        // Get all active connection IDs for these users
        var connectionIds = await GetConnectionsDbSet()
            .Where(c => userIdList.Contains(c.UserId) && c.Connected)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        if (!connectionIds.Any())
        {
            _logger.LogWarning("No active connections found for the specified users");
            return;
        }

        // Send to all connections for these users
        await _hubContext.Clients.Clients(connectionIds).SendAsync(method, data);
        _logger.LogDebug("Sent '{Method}' to {Count} connections for {UserCount} users",
            method, connectionIds.Count, userIdList.Count);
    }

    /// <inheritdoc/>
    public int GetConnectedUsersCount()
    {
        return GetConnectedUsersDbSet()
            .Count(u => u.Connections.Any(c => c.Connected));
    }

    /// <inheritdoc/>
    public int GetActiveConnectionsCount()
    {
        return GetConnectionsDbSet()
            .Count(c => c.Connected);
    }

    /// <inheritdoc/>
    public bool IsUserConnected(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        return GetConnectionsDbSet()
            .Any(c => c.UserId == userId && c.Connected);
    }

    /// <summary>
    /// Gets the ConnectedUsers DbSet from the context
    /// </summary>
    protected virtual DbSet<ConnectedUser> GetConnectedUsersDbSet()
    {
        return _context.Set<ConnectedUser>();
    }

    /// <summary>
    /// Gets the Connections DbSet from the context
    /// </summary>
    protected virtual DbSet<Connection> GetConnectionsDbSet()
    {
        return _context.Set<Connection>();
    }
}
