using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nesco.SignalRUserManagement.Core.Models;
using Nesco.SignalRUserManagement.Core.Options;
using Nesco.SignalRUserManagement.Server.Models;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Models;
using System.Text.Json;

namespace Nesco.SignalRUserManagement.Server.Hubs;

/// <summary>
/// Base hub for user connection management with database persistence
/// Inherit from this hub in your application
/// </summary>
/// <typeparam name="TDbContext">Your DbContext type that contains ConnectedUsers and Connections DbSets</typeparam>
[Authorize(AuthenticationSchemes = "Bearer")]
public class UserManagementHub<TDbContext> : Hub where TDbContext : DbContext
{
    protected readonly TDbContext DbContext;
    protected readonly ILogger<UserManagementHub<TDbContext>> Logger;
    protected readonly UserManagementOptions Options;
    private readonly IResponseManager? _responseManager;

    public UserManagementHub(
        TDbContext context,
        ILogger<UserManagementHub<TDbContext>> logger,
        IOptions<UserManagementOptions> options,
        IResponseManager? responseManager = null)
    {
        DbContext = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _responseManager = responseManager; // Optional for SignalR Communicator integration
    }

    /// <summary>
    /// Returns the current connection ID
    /// </summary>
    public new string GetConnectionId() => Context.ConnectionId;

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        Logger.LogInformation(">>> [HUB OnConnectedAsync] New connection established! ConnectionId: {ConnectionId}, UserId: {UserId}, IsAuthenticated: {IsAuthenticated}",
            Context.ConnectionId, userId ?? "(anonymous)", Context.User?.Identity?.IsAuthenticated ?? false);

        if (string.IsNullOrEmpty(userId))
        {
            Logger.LogWarning(">>> [HUB OnConnectedAsync] User connected without identifier: {ConnectionId}, IsAuthenticated: {IsAuthenticated}",
                Context.ConnectionId, Context.User?.Identity?.IsAuthenticated ?? false);

            // Log all claims for debugging
            if (Context.User?.Claims != null)
            {
                Logger.LogWarning(">>> [HUB OnConnectedAsync] Available claims for ConnectionId {ConnectionId}:", Context.ConnectionId);
                foreach (var claim in Context.User.Claims)
                {
                    Logger.LogWarning(">>>   - {ClaimType}: {ClaimValue}", claim.Type, claim.Value);
                }
            }
            else
            {
                Logger.LogWarning(">>> [HUB OnConnectedAsync] No claims available for ConnectionId {ConnectionId}", Context.ConnectionId);
            }

            await base.OnConnectedAsync();
            return;
        }

        Logger.LogInformation(">>> [HUB OnConnectedAsync] User {UserId} connecting with connection {ConnectionId}", userId, Context.ConnectionId);

        try
        {
            // Clean up STALE connections only (not all connections!)
            // This handles cases where OnDisconnectedAsync didn't fire (page refresh, crash, network issues)
            // A connection is considered stale if:
            // 1. It's marked as disconnected (Connected = false), OR
            // 2. It's older than 60 seconds (likely a zombie connection)
            var staleThreshold = DateTime.UtcNow.AddSeconds(-60);

            var staleConnections = await GetConnectionsDbSet()
                .Where(c => c.UserId == userId && (!c.Connected || c.ConnectedAt < staleThreshold))
                .ToListAsync();

            if (staleConnections.Any())
            {
                Logger.LogWarning(">>> [HUB OnConnectedAsync] FOUND {Count} STALE connections for user {UserId}! Removing: [{Connections}]",
                    staleConnections.Count, userId, string.Join(", ", staleConnections.Select(c => c.ConnectionId)));
                GetConnectionsDbSet().RemoveRange(staleConnections);
                // Save immediately to ensure they're removed before we add the new one
                await DbContext.SaveChangesAsync();

                // IMPORTANT: Clear change tracker to ensure removed entities don't reappear
                DbContext.ChangeTracker.Clear();

                Logger.LogInformation(">>> [HUB OnConnectedAsync] Removed {Count} stale connections successfully and cleared change tracker", staleConnections.Count);
            }
            else
            {
                Logger.LogInformation(">>> [HUB OnConnectedAsync] No stale connections found for user {UserId}", userId);
            }

            // Check if this exact connection ID already exists (shouldn't happen, but just in case)
            var duplicateConnection = await GetConnectionsDbSet()
                .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId);

            if (duplicateConnection != null)
            {
                Logger.LogWarning(">>> [HUB OnConnectedAsync] WARNING: Connection {ConnectionId} already exists in database! Removing duplicate.", Context.ConnectionId);
                GetConnectionsDbSet().Remove(duplicateConnection);
                await DbContext.SaveChangesAsync();
                DbContext.ChangeTracker.Clear();
            }

            // Get or create user (query fresh from database after clearing tracker)
            var user = await GetConnectedUsersDbSet()
                .Include(u => u.Connections)
                .SingleOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                Logger.LogInformation(">>> [HUB OnConnectedAsync] Creating new ConnectedUser for {UserId}", userId);
                user = new ConnectedUser
                {
                    UserId = userId,
                    LastConnect = DateTime.UtcNow,
                    Connections = new List<Connection>()
                };
                GetConnectedUsersDbSet().Add(user);
            }
            else
            {
                Logger.LogInformation(">>> [HUB OnConnectedAsync] Updating existing ConnectedUser for {UserId}", userId);
                user.LastConnect = DateTime.UtcNow;
            }

            // Get user agent if tracking is enabled
            var userAgent = string.Empty;
            if (Options.TrackUserAgent)
            {
                userAgent = Context.GetHttpContext()?.Request.Headers["user-agent"] ?? StringValues.Empty;
            }

            // Add new connection
            Logger.LogInformation(">>> [HUB OnConnectedAsync] Adding NEW connection: ConnectionId={ConnectionId}, UserId={UserId}",
                Context.ConnectionId, userId);

            user.Connections.Add(new Connection
            {
                ConnectionId = Context.ConnectionId,
                UserAgent = userAgent,
                Connected = true,
                ConnectedAt = DateTime.UtcNow,
                UserId = userId
            });

            // Purge offline connections if enabled
            if (Options.AutoPurgeOfflineConnections)
            {
                Logger.LogInformation(">>> [HUB OnConnectedAsync] Running AutoPurge for offline connections...");
                await PurgeOfflineConnections();
            }

            await DbContext.SaveChangesAsync();

            Logger.LogInformation(">>> [HUB OnConnectedAsync] ✓ User {UserId} connected successfully with ConnectionId: {ConnectionId}",
                userId, Context.ConnectionId);

            // Broadcast connection event if enabled
            if (Options.BroadcastConnectionEvents)
            {
                var eventArgs = new UserConnectionEventArgs
                {
                    UserId = userId,
                    ConnectionId = Context.ConnectionId,
                    UserAgent = userAgent,
                    EventType = UserConnectionEventType.Connected,
                    Timestamp = DateTime.UtcNow
                };

                await Clients.All.SendAsync(Options.ConnectionEventMethod, eventArgs);
            }

            Logger.LogInformation("User {UserId} connected successfully", userId);

            // Call custom callback if provided
            if (Options.OnUserConnected != null)
            {
                await Options.OnUserConnected(Clients, userId, Context.ConnectionId, Context);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling user connection for {UserId}", userId);
            throw;
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        Logger.LogInformation(">>> [HUB OnDisconnectedAsync] Disconnect triggered for ConnectionId: {ConnectionId}, UserId: {UserId}, HasException: {HasException}",
            connectionId, userId ?? "(anonymous)", exception != null);

        if (exception != null)
        {
            Logger.LogWarning(exception, ">>> [HUB OnDisconnectedAsync] Disconnect was triggered by exception for ConnectionId: {ConnectionId}", connectionId);
        }

        if (string.IsNullOrEmpty(userId))
        {
            Logger.LogWarning(">>> [HUB OnDisconnectedAsync] No userId found for ConnectionId: {ConnectionId}, skipping cleanup", connectionId);
            await base.OnDisconnectedAsync(exception);
            return;
        }

        Logger.LogInformation(">>> [HUB OnDisconnectedAsync] Processing disconnect for User {UserId} from connection {ConnectionId}", userId, connectionId);

        try
        {
            // Call custom callback if provided (before disconnection)
            if (Options.OnUserDisconnected != null)
            {
                await Options.OnUserDisconnected(Clients, userId, connectionId, Context);
            }

            // Update user last disconnect time
            var user = await GetConnectedUsersDbSet()
                .SingleOrDefaultAsync(u => u.UserId == userId);

            if (user != null)
            {
                user.LastDisconnect = DateTime.UtcNow;
                Logger.LogInformation(">>> [HUB OnDisconnectedAsync] Updated LastDisconnect for user {UserId}", userId);
            }
            else
            {
                Logger.LogWarning(">>> [HUB OnDisconnectedAsync] User {UserId} not found in database", userId);
            }

            // Remove the connection - using direct query to avoid tracking issues
            var connection = await GetConnectionsDbSet()
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

            if (connection != null)
            {
                Logger.LogInformation(">>> [HUB OnDisconnectedAsync] Found connection {ConnectionId} for user {UserId}, removing from database",
                    connectionId, userId);
                GetConnectionsDbSet().Remove(connection);
            }
            else
            {
                Logger.LogWarning(">>> [HUB OnDisconnectedAsync] Connection {ConnectionId} not found in database for user {UserId}",
                    connectionId, userId);
            }

            var saveResult = await DbContext.SaveChangesAsync();
            Logger.LogInformation(">>> [HUB OnDisconnectedAsync] SaveChanges completed. Entities modified: {Count}", saveResult);

            // Verify the connection was actually removed
            var verifyConnection = await GetConnectionsDbSet()
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
            if (verifyConnection != null)
            {
                Logger.LogError(">>> [HUB OnDisconnectedAsync] ERROR: Connection {ConnectionId} still exists after deletion!", connectionId);
            }
            else
            {
                Logger.LogInformation(">>> [HUB OnDisconnectedAsync] ✓ Verified connection {ConnectionId} was successfully removed", connectionId);
            }

            // Broadcast disconnection event if enabled
            if (Options.BroadcastConnectionEvents)
            {
                var eventArgs = new UserConnectionEventArgs
                {
                    UserId = userId,
                    ConnectionId = connectionId,
                    EventType = UserConnectionEventType.Disconnected,
                    Timestamp = DateTime.UtcNow
                };

                await Clients.All.SendAsync(Options.ConnectionEventMethod, eventArgs);
            }

            Logger.LogInformation(">>> [HUB OnDisconnectedAsync] ✓ User {UserId} disconnected successfully from connection {ConnectionId}", userId, connectionId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ">>> [HUB OnDisconnectedAsync ERROR] Error handling user disconnection for {UserId}, ConnectionId: {ConnectionId}", userId, connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Purges offline connections from the database
    /// </summary>
    protected virtual async Task PurgeOfflineConnections()
    {
        var offlineConnections = await GetConnectionsDbSet()
            .Where(c => !c.Connected)
            .ToListAsync();

        if (offlineConnections.Any())
        {
            GetConnectionsDbSet().RemoveRange(offlineConnections);
            Logger.LogDebug("Purged {Count} offline connections", offlineConnections.Count);
        }
    }

    /// <summary>
    /// Gets the ConnectedUsers DbSet from the context
    /// Override this if your DbSet has a different name
    /// </summary>
    protected virtual DbSet<ConnectedUser> GetConnectedUsersDbSet()
    {
        return DbContext.Set<ConnectedUser>();
    }

    /// <summary>
    /// Gets the Connections DbSet from the context
    /// Override this if your DbSet has a different name
    /// </summary>
    protected virtual DbSet<Connection> GetConnectionsDbSet()
    {
        return DbContext.Set<Connection>();
    }

    /// <summary>
    /// Handles response from SignalR Communicator clients.
    /// This method enables SignalR Communicator to share this hub.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="result">The result object from the client.</param>
    public async Task HandleResponse(string requestId, object? result)
    {
        Logger.LogInformation(">>> [HUB HandleResponse] Received response for RequestId: {RequestId}, ConnectionId: {ConnectionId}, ResultType: {ResultType}",
            requestId, Context.ConnectionId, result?.GetType().Name ?? "null");

        if (_responseManager == null)
        {
            Logger.LogWarning(">>> [HUB HandleResponse] ResponseManager is not available. Did you forget to use AddSignalRCommunicatorServerWithHub?");
            return;
        }

        try
        {
            // Deserialize the response DTO
            SignalRResponse? responseDto = null;

            if (result is JsonElement jsonElement)
            {
                Logger.LogInformation(">>> [HUB HandleResponse] Deserializing JsonElement for RequestId: {RequestId}", requestId);
                responseDto = JsonSerializer.Deserialize<SignalRResponse>(jsonElement.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else if (result is SignalRResponse directDto)
            {
                Logger.LogInformation(">>> [HUB HandleResponse] Using direct SignalRResponse for RequestId: {RequestId}", requestId);
                responseDto = directDto;
            }

            if (responseDto == null)
            {
                Logger.LogInformation(">>> [HUB HandleResponse] Creating null response for RequestId: {RequestId}", requestId);
                responseDto = new SignalRResponse
                {
                    ResponseType = SignalRResponseType.Null
                };
            }

            Logger.LogInformation(">>> [HUB HandleResponse] Response processed: RequestId={RequestId}, ResponseType={ResponseType}",
                requestId, responseDto.ResponseType);

            // Forward to ResponseManager to complete the request
            Logger.LogInformation(">>> [HUB HandleResponse] Forwarding to ResponseManager for RequestId: {RequestId}", requestId);
            if (!_responseManager.CompleteRequest(requestId, responseDto))
            {
                Logger.LogWarning(">>> [HUB HandleResponse] Received response for unknown request ID: {RequestId}", requestId);
            }
            else
            {
                Logger.LogInformation(">>> [HUB HandleResponse] Successfully completed request: {RequestId}", requestId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ">>> [HUB HandleResponse ERROR] Error processing client response for request {RequestId}", requestId);

            // Try to send error response to complete the request
            var errorResponse = new SignalRResponse
            {
                ResponseType = SignalRResponseType.Error,
                ErrorMessage = ex.Message
            };
            _responseManager.CompleteRequest(requestId, errorResponse);
        }

        await Task.CompletedTask;
    }
}
