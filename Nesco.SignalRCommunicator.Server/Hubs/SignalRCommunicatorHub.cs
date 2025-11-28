using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Claims;

namespace Nesco.SignalRCommunicator.Server.Hubs;

/// <summary>
/// SignalR hub that manages client connections and handles responses from clients.
/// </summary>
public class SignalRCommunicatorHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> Connections = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new();
    protected readonly ILogger<SignalRCommunicatorHub> _logger;
    private readonly IResponseManager _responseManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRCommunicatorHub"/> class.
    /// </summary>
    public SignalRCommunicatorHub(ILogger<SignalRCommunicatorHub> logger, IResponseManager responseManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _responseManager = responseManager ?? throw new ArgumentNullException(nameof(responseManager));
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";

        // Add connection
        Connections.TryAdd(Context.ConnectionId, username);

        // Track user to connection mapping
        UserConnections.AddOrUpdate(
            userId,
            new HashSet<string> { Context.ConnectionId },
            (_, existingSet) =>
            {
                lock (existingSet)
                {
                    existingSet.Add(Context.ConnectionId);
                }
                return existingSet;
            });

        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId} ({Username})",
            Context.ConnectionId, userId, username);

        return base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        // Remove connection
        Connections.TryRemove(Context.ConnectionId, out _);

        // Remove from user connections
        if (UserConnections.TryGetValue(userId, out var connectionSet))
        {
            lock (connectionSet)
            {
                connectionSet.Remove(Context.ConnectionId);

                // If user has no more connections, remove the user entry
                if (connectionSet.Count == 0)
                {
                    UserConnections.TryRemove(userId, out _);
                }
            }
        }

        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}",
            Context.ConnectionId, userId);

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gets the number of currently connected clients.
    /// </summary>
    public static int ConnectedClientsCount => Connections.Count;

    /// <summary>
    /// Gets the connection IDs for a specific user.
    /// </summary>
    /// <param name="userId">The user ID to get connections for.</param>
    /// <returns>List of connection IDs for the user.</returns>
    public static List<string> GetUserConnectionIds(string userId)
    {
        if (UserConnections.TryGetValue(userId, out var connectionSet))
        {
            lock (connectionSet)
            {
                return new List<string>(connectionSet);
            }
        }
        return new List<string>();
    }

    /// <summary>
    /// Gets all connected user IDs with their connection information.
    /// </summary>
    /// <returns>Dictionary mapping user IDs to their connection IDs.</returns>
    public static Dictionary<string, List<string>> GetAllUserConnections()
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var kvp in UserConnections)
        {
            lock (kvp.Value)
            {
                result[kvp.Key] = new List<string>(kvp.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// Called by clients to send response back with requestId.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="result">The result object from the client.</param>
    public async Task HandleResponse(string requestId, object? result)
    {
        try
        {
            // Deserialize the response DTO
            SignalRResponse? responseDto = null;

            if (result is JsonElement jsonElement)
            {
                responseDto = JsonSerializer.Deserialize<SignalRResponse>(jsonElement.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else if (result is SignalRResponse directDto)
            {
                responseDto = directDto;
            }

            if (responseDto == null)
            {
                responseDto = new SignalRResponse
                {
                    ResponseType = SignalRResponseType.Null
                };
            }

            _logger.LogDebug("Received response from client of type {ResponseType} for request ID: {RequestId}",
                responseDto.ResponseType, requestId);

            // Forward to ResponseManager to complete the request
            if (!_responseManager.CompleteRequest(requestId, responseDto))
            {
                _logger.LogWarning("Received response for unknown request ID: {RequestId}", requestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing client response for request {RequestId}", requestId);

            // Try to send error response to complete the request
            var errorResponse = new SignalRResponse
            {
                ResponseType = SignalRResponseType.Error,
                ErrorMessage = ex.Message
            };
            _responseManager.CompleteRequest(requestId, errorResponse);
        }
    }
}
