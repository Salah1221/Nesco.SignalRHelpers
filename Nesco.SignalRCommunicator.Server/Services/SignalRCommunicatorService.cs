using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Core.Options;
using Nesco.SignalRCommunicator.Server.Hubs;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Nesco.SignalRCommunicator.Server.Services;

/// <summary>
/// Provides methods for invoking methods on connected SignalR clients and receiving responses.
/// </summary>
public interface ISignalRCommunicatorService
{
    /// <summary>
    /// Invokes a method on all connected clients and waits for the first response.
    /// </summary>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The response from the client.</returns>
    Task<SignalRResponse> InvokeMethodAsync(string methodName, object? parameter);

    /// <summary>
    /// Invokes a method on all connected clients, waits for response, and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The deserialized response, or null if the response was null or an error occurred.</returns>
    Task<T?> InvokeMethodAsync<T>(string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a specific connection and waits for response.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID to target.</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The response from the client.</returns>
    Task<SignalRResponse> InvokeMethodOnConnectionAsync(string connectionId, string methodName, object? parameter);

    /// <summary>
    /// Invokes a method on a specific connection, waits for response, and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="connectionId">The SignalR connection ID to target.</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The deserialized response, or null if the response was null or an error occurred.</returns>
    Task<T?> InvokeMethodOnConnectionAsync<T>(string connectionId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on all connections of a specific user and waits for the first response.
    /// </summary>
    /// <param name="userId">The user ID to target (all their connections).</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The response from the client.</returns>
    Task<SignalRResponse> InvokeMethodOnUserAsync(string userId, string methodName, object? parameter);

    /// <summary>
    /// Invokes a method on all connections of a specific user, waits for response, and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="userId">The user ID to target (all their connections).</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The deserialized response, or null if the response was null or an error occurred.</returns>
    Task<T?> InvokeMethodOnUserAsync<T>(string userId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on all connections of multiple users and waits for the first response.
    /// </summary>
    /// <param name="userIds">The user IDs to target (all their connections).</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The response from the client.</returns>
    Task<SignalRResponse> InvokeMethodOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter);

    /// <summary>
    /// Invokes a method on all connections of multiple users, waits for response, and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="userIds">The user IDs to target (all their connections).</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The deserialized response, or null if the response was null or an error occurred.</returns>
    Task<T?> InvokeMethodOnUsersAsync<T>(IEnumerable<string> userIds, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a list of specific connections and waits for the first response.
    /// </summary>
    /// <param name="connectionIds">The SignalR connection IDs to target.</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The response from the client.</returns>
    Task<SignalRResponse> InvokeMethodOnConnectionsAsync(IEnumerable<string> connectionIds, string methodName, object? parameter);

    /// <summary>
    /// Invokes a method on a list of specific connections, waits for response, and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="connectionIds">The SignalR connection IDs to target.</param>
    /// <param name="methodName">The name of the method to invoke.</param>
    /// <param name="parameter">The parameter to pass to the method.</param>
    /// <returns>The deserialized response, or null if the response was null or an error occurred.</returns>
    Task<T?> InvokeMethodOnConnectionsAsync<T>(IEnumerable<string> connectionIds, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Checks if there are any connected clients.
    /// </summary>
    /// <returns>True if there are connected clients, false otherwise.</returns>
    bool HasConnectedClients();
}

/// <summary>
/// Base implementation of the SignalR communicator service that works with any hub.
/// </summary>
/// <typeparam name="THub">The hub type to use for communication.</typeparam>
public class SignalRCommunicatorService<THub> : ISignalRCommunicatorService
    where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;
    private readonly IFileReaderService _fileReaderService;
    private readonly ILogger<SignalRCommunicatorService<THub>> _logger;
    private readonly SignalRServerOptions _options;
    private readonly IResponseManager _responseManager;
    private readonly SemaphoreSlim _requestSemaphore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRCommunicatorService{THub}"/> class.
    /// </summary>
    public SignalRCommunicatorService(
        IHubContext<THub> hubContext,
        IFileReaderService fileReaderService,
        ILogger<SignalRCommunicatorService<THub>> logger,
        IOptions<SignalRServerOptions> options,
        IResponseManager responseManager)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _fileReaderService = fileReaderService ?? throw new ArgumentNullException(nameof(fileReaderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _responseManager = responseManager ?? throw new ArgumentNullException(nameof(responseManager));
        _requestSemaphore = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    /// <inheritdoc/>
    public async Task<SignalRResponse> InvokeMethodAsync(string methodName, object? parameter)
    {
        _logger.LogInformation(">>> [InvokeMethodAsync] Starting invocation of {MethodName} to all clients", methodName);
        return await InvokeMethodInternalAsync(
            async (requestId) =>
            {
                _logger.LogInformation(">>> [InvokeMethodAsync] Sending InvokeMethod message to all clients with RequestId: {RequestId}", requestId);
                await _hubContext.Clients.All.SendAsync("InvokeMethod", requestId, methodName, parameter);
                _logger.LogInformation(">>> [InvokeMethodAsync] Message sent to all clients with RequestId: {RequestId}", requestId);
            },
            methodName,
            "all clients");
    }

    /// <inheritdoc/>
    public async Task<T?> InvokeMethodAsync<T>(string methodName, object? parameter) where T : class
    {
        var response = await InvokeMethodAsync(methodName, parameter);
        return await DecodeResponse<T>(response);
    }

    /// <inheritdoc/>
    public async Task<SignalRResponse> InvokeMethodOnConnectionAsync(string connectionId, string methodName, object? parameter)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        _logger.LogInformation(">>> [InvokeMethodOnConnectionAsync] Starting invocation of {MethodName} to connection {ConnectionId}", methodName, connectionId);
        return await InvokeMethodInternalAsync(
            async (requestId) =>
            {
                _logger.LogInformation(">>> [InvokeMethodOnConnectionAsync] Sending InvokeMethod message to connection {ConnectionId} with RequestId: {RequestId}", connectionId, requestId);
                await _hubContext.Clients.Client(connectionId).SendAsync("InvokeMethod", requestId, methodName, parameter);
                _logger.LogInformation(">>> [InvokeMethodOnConnectionAsync] Message sent to connection {ConnectionId} with RequestId: {RequestId}", connectionId, requestId);
            },
            methodName,
            $"connection {connectionId}");
    }

    /// <inheritdoc/>
    public async Task<T?> InvokeMethodOnConnectionAsync<T>(string connectionId, string methodName, object? parameter) where T : class
    {
        var response = await InvokeMethodOnConnectionAsync(connectionId, methodName, parameter);
        return await DecodeResponse<T>(response);
    }

    /// <inheritdoc/>
    public async Task<SignalRResponse> InvokeMethodOnUserAsync(string userId, string methodName, object? parameter)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        _logger.LogInformation(">>> [InvokeMethodOnUserAsync] Starting invocation of {MethodName} to user {UserId}", methodName, userId);
        // Use SignalR's built-in user targeting (works when hub has authenticated users)
        // This will target all connections for this user
        return await InvokeMethodInternalAsync(
            async (requestId) =>
            {
                _logger.LogInformation(">>> [InvokeMethodOnUserAsync] Sending InvokeMethod message to user {UserId} with RequestId: {RequestId}", userId, requestId);
                await _hubContext.Clients.User(userId).SendAsync("InvokeMethod", requestId, methodName, parameter);
                _logger.LogInformation(">>> [InvokeMethodOnUserAsync] Message sent to user {UserId} with RequestId: {RequestId}", userId, requestId);
            },
            methodName,
            $"user {userId}");
    }

    /// <inheritdoc/>
    public async Task<T?> InvokeMethodOnUserAsync<T>(string userId, string methodName, object? parameter) where T : class
    {
        var response = await InvokeMethodOnUserAsync(userId, methodName, parameter);
        return await DecodeResponse<T>(response);
    }

    /// <inheritdoc/>
    public async Task<SignalRResponse> InvokeMethodOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter)
    {
        if (userIds == null || !userIds.Any())
            throw new ArgumentException("User IDs cannot be null or empty", nameof(userIds));

        var userIdList = userIds.ToList();

        // Use SignalR's built-in user targeting for multiple users
        return await InvokeMethodInternalAsync(
            async (requestId) => await _hubContext.Clients.Users(userIdList).SendAsync("InvokeMethod", requestId, methodName, parameter),
            methodName,
            $"users ({string.Join(", ", userIdList.Take(3))}{(userIdList.Count > 3 ? "..." : "")})");
    }

    /// <inheritdoc/>
    public async Task<T?> InvokeMethodOnUsersAsync<T>(IEnumerable<string> userIds, string methodName, object? parameter) where T : class
    {
        var response = await InvokeMethodOnUsersAsync(userIds, methodName, parameter);
        return await DecodeResponse<T>(response);
    }

    /// <inheritdoc/>
    public async Task<SignalRResponse> InvokeMethodOnConnectionsAsync(IEnumerable<string> connectionIds, string methodName, object? parameter)
    {
        if (connectionIds == null || !connectionIds.Any())
            throw new ArgumentException("Connection IDs cannot be null or empty", nameof(connectionIds));

        var connectionIdList = connectionIds.ToList();
        _logger.LogInformation(">>> [InvokeMethodOnConnectionsAsync] Starting invocation of {MethodName} to {Count} connections: [{ConnectionIds}]",
            methodName, connectionIdList.Count, string.Join(", ", connectionIdList));

        return await InvokeMethodInternalAsync(
            async (requestId) =>
            {
                _logger.LogInformation(">>> [InvokeMethodOnConnectionsAsync] Sending InvokeMethod to {Count} connections with RequestId: {RequestId}",
                    connectionIdList.Count, requestId);
                await _hubContext.Clients.Clients(connectionIdList).SendAsync("InvokeMethod", requestId, methodName, parameter);
                _logger.LogInformation(">>> [InvokeMethodOnConnectionsAsync] Message sent to {Count} connections with RequestId: {RequestId}",
                    connectionIdList.Count, requestId);
            },
            methodName,
            $"connections ({string.Join(", ", connectionIdList.Take(3))}{(connectionIdList.Count > 3 ? "..." : "")})");
    }

    /// <inheritdoc/>
    public async Task<T?> InvokeMethodOnConnectionsAsync<T>(IEnumerable<string> connectionIds, string methodName, object? parameter) where T : class
    {
        var response = await InvokeMethodOnConnectionsAsync(connectionIds, methodName, parameter);
        return await DecodeResponse<T>(response);
    }

    /// <summary>
    /// Internal method that handles the common logic for invoking methods on clients.
    /// </summary>
    private async Task<SignalRResponse> InvokeMethodInternalAsync(
        Func<string, Task> sendAction,
        string methodName,
        string target)
    {
        _logger.LogInformation(">>> [InvokeMethodInternalAsync] Waiting for semaphore...");
        // Try to acquire the semaphore - will wait if all slots are busy
        if (!await _requestSemaphore.WaitAsync(TimeSpan.FromSeconds(_options.SemaphoreTimeoutSeconds)))
        {
            throw new InvalidOperationException($"Server has reached maximum concurrent requests ({_options.MaxConcurrentRequests}). Please try again later.");
        }

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<SignalRResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Invoking method {MethodName} on {Target} with RequestId: {RequestId}",
                methodName, target, requestId);

            // Register the request with the response manager
            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Registering request {RequestId} with ResponseManager", requestId);
            _responseManager.RegisterRequest(requestId, tcs);

            // Send the method invocation with requestId to target clients
            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Executing sendAction for RequestId: {RequestId}", requestId);
            await sendAction(requestId);

            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Method {MethodName} sent to {Target} with RequestId: {RequestId}. Now waiting for response...",
                methodName, target, requestId);

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
            cts.Token.Register(() =>
            {
                _logger.LogWarning(">>> [InvokeMethodInternalAsync] Timeout triggered for RequestId: {RequestId}", requestId);
                _responseManager.RemoveRequest(requestId);
                tcs.TrySetCanceled();
            });

            var result = await tcs.Task;
            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Received response for RequestId: {RequestId}", requestId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(">>> [InvokeMethodInternalAsync] Request {RequestId} to {MethodName} on {Target} timed out after {Timeout} seconds",
                requestId, methodName, target, _options.RequestTimeoutSeconds);
            throw new TimeoutException($"Request to {methodName} on {target} timed out after {_options.RequestTimeoutSeconds} seconds");
        }
        finally
        {
            _logger.LogInformation(">>> [InvokeMethodInternalAsync] Cleaning up request {RequestId}", requestId);
            _responseManager.RemoveRequest(requestId);
            _requestSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public bool HasConnectedClients()
    {
        // For generic implementation, we can't easily track this
        // Return true by default, let the invoke method handle timeouts
        return true;
    }


    private async Task<T?> DecodeResponse<T>(SignalRResponse response) where T : class
    {
        if (response == null) return null;

        // If ErrorMessage is set, log it and return null
        if (!string.IsNullOrEmpty(response.ErrorMessage))
        {
            _logger.LogError("Client returned error: {ErrorMessage}", response.ErrorMessage);
            return null;
        }

        // If Result is JsonObject, deserialize it directly
        if (response.ResponseType == SignalRResponseType.JsonObject)
        {
            return DeserializeResult<T>(response.JsonData);
        }

        // If FilePath is provided, read the file and deserialize its content
        if (!string.IsNullOrEmpty(response.FilePath))
        {
            return await HandleFilePathResponse<T>(response.FilePath);
        }

        return null;
    }

    private async Task<T?> HandleFilePathResponse<T>(string filePath) where T : class
    {
        try
        {
            if (!await _fileReaderService.FileExistsAsync(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                return null;
            }

            _logger.LogInformation("Reading response from file: {FilePath}", filePath);
            var jsonContent = await _fileReaderService.ReadFileAsync(filePath);

            // Delete temporary files after reading if configured
            if (_options.AutoDeleteTempFiles && filePath.Contains(_options.TempFolder))
            {
                try
                {
                    await _fileReaderService.DeleteFileAsync(filePath);
                    _logger.LogDebug("Deleted temporary file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}", filePath);
                }
            }

            // Deserialize the file content
            return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file response {FilePath}", filePath);
            throw;
        }
    }

    private T? DeserializeResult<T>(object? result) where T : class
    {
        if (result == null) return null;

        if (result is T directCast)
        {
            return directCast;
        }

        // Handle JsonElement that might contain a string JSON
        if (result is JsonElement jsonElement)
        {
            try
            {
                // Check if the JsonElement is a string containing JSON
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var jsonStr = jsonElement.GetString();
                    if (!string.IsNullOrEmpty(jsonStr))
                    {
                        // Parse the string as JSON and then deserialize
                        var parsedJson = JsonDocument.Parse(jsonStr);
                        return JsonSerializer.Deserialize<T>(parsedJson.RootElement.GetRawText(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                else
                {
                    // Direct deserialization if not a string
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JsonElement to type {TypeName}", typeof(T).Name);
                return null;
            }
        }

        // Handle plain string JSON
        if (result is string jsonString)
        {
            try
            {
                // First try to parse it as a JSON document
                var doc = JsonDocument.Parse(jsonString);
                return JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize string result to type {TypeName}", typeof(T).Name);
                return null;
            }
        }

        // Try to convert via JSON serialization/deserialization
        try
        {
            var json = JsonSerializer.Serialize(result);
            // Check if the serialized result is a JSON string that needs parsing
            if (json.StartsWith("\"") && json.EndsWith("\""))
            {
                // It's a quoted string, so deserialize it first to get the actual JSON string
                var actualJson = JsonSerializer.Deserialize<string>(json);
                if (!string.IsNullOrEmpty(actualJson))
                {
                    return JsonSerializer.Deserialize<T>(actualJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            else
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert result to type {TypeName}", typeof(T).Name);
            return null;
        }

        return null;
    }
}

/// <summary>
/// Non-generic implementation for backward compatibility with SignalRCommunicatorHub.
/// </summary>
public class SignalRCommunicatorService : SignalRCommunicatorService<SignalRCommunicatorHub>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRCommunicatorService"/> class.
    /// </summary>
    public SignalRCommunicatorService(
        IHubContext<SignalRCommunicatorHub> hubContext,
        IFileReaderService fileReaderService,
        ILogger<SignalRCommunicatorService<SignalRCommunicatorHub>> logger,
        IOptions<SignalRServerOptions> options,
        IResponseManager responseManager)
        : base(hubContext, fileReaderService, logger, options, responseManager)
    {
    }
}
