using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Core.Options;
using System.Text.Json;

namespace Nesco.SignalRCommunicator.Client.Services;

/// <summary>
/// Provides SignalR client functionality for receiving and responding to method invocations from the server.
/// </summary>
public interface ISignalRCommunicatorClient
{
    /// <summary>
    /// Starts the SignalR connection to the server.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets whether the client is currently connected to the server.
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// Implementation of the SignalR communicator client.
/// Manages connection lifecycle, receives method invocations, executes them, and returns responses.
/// </summary>
public sealed class SignalRCommunicatorClient : ISignalRCommunicatorClient, IDisposable
{
    private readonly ILogger<SignalRCommunicatorClient> _logger;
    private readonly IFileUploadService _fileUploadService;
    private readonly IMethodExecutor _methodExecutor;
    private readonly SignalRClientOptions _options;
    private HubConnection? _connection;
    private bool _disposed = false;
    private bool _isSharedConnection = false; // Track if connection is shared (not owned)
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly System.Text.Encoding Utf8Encoding = System.Text.Encoding.UTF8;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRCommunicatorClient"/> class.
    /// </summary>
    public SignalRCommunicatorClient(
        ILogger<SignalRCommunicatorClient> logger,
        IFileUploadService fileUploadService,
        IMethodExecutor methodExecutor,
        IOptions<SignalRClientOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileUploadService = fileUploadService ?? throw new ArgumentNullException(nameof(fileUploadService));
        _methodExecutor = methodExecutor ?? throw new ArgumentNullException(nameof(methodExecutor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Starts the SignalR connection with optional authentication token provider.
    /// </summary>
    /// <param name="accessTokenProvider">Optional function to provide access token for authentication.</param>
    public async Task StartAsync(Func<Task<string>>? accessTokenProvider = null)
    {
        await StartInternalAsync(accessTokenProvider);
    }

    /// <summary>
    /// Uses an existing HubConnection instead of creating a new one.
    /// This allows sharing a connection with UserManagementClient.
    /// </summary>
    /// <param name="existingConnection">The existing HubConnection to use.</param>
    public void UseExistingConnection(HubConnection existingConnection)
    {
        if (existingConnection == null)
            throw new ArgumentNullException(nameof(existingConnection));

        _logger.LogInformation(">>> [UseExistingConnection] Using existing connection: {ConnectionId}, State: {State}",
            existingConnection.ConnectionId, existingConnection.State);

        _connection = existingConnection;
        _isSharedConnection = true; // Mark as shared - we don't own this connection

        // Register handlers for method calls from the server
        RegisterMethodHandlers();

        _logger.LogInformation(">>> [UseExistingConnection] Registered handlers on SHARED connection (will not dispose on stop)");
    }

    /// <inheritdoc/>
    async Task ISignalRCommunicatorClient.StartAsync()
    {
        await StartInternalAsync(null);
    }

    private async Task StartInternalAsync(Func<Task<string>>? accessTokenProvider)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot start SignalR connection - service has been disposed");
            return;
        }

        await _connectionSemaphore.WaitAsync();
        try
        {
            await StartConnectionInternalAsync(accessTokenProvider);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task StartConnectionInternalAsync(Func<Task<string>>? accessTokenProvider)
    {
        _logger.LogInformation(">>> [StartConnectionInternalAsync] Starting new OWNED connection (not shared)");

        // Dispose existing connection if it exists
        if (_connection != null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing existing SignalR connection");
            }
        }

        _isSharedConnection = false; // This will be our own connection

        var serverUrl = _options.ServerUrl;
        var hubUrl = $"{serverUrl.TrimEnd('/')}{_options.HubPath}";

        var connectionBuilder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (accessTokenProvider != null)
                {
                    options.AccessTokenProvider = async () => await accessTokenProvider();
                }
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            });

        _connection = connectionBuilder.Build();

        // Configure client-side timeouts using options
        _connection.KeepAliveInterval = TimeSpan.FromSeconds(_options.KeepAliveIntervalSeconds);
        _connection.ServerTimeout = TimeSpan.FromSeconds(_options.ServerTimeoutSeconds);

        // Set up connection event handlers
        _connection.Closed += OnConnectionClosed;

        // Register handlers for method calls from the server
        RegisterMethodHandlers();

        var retryCount = 0;
        while (retryCount <= _options.MaxRetryAttempts)
        {
            try
            {
                _logger.LogInformation(">>> [StartConnectionInternalAsync] Attempting to start connection to {HubUrl}...", hubUrl);
                await _connection.StartAsync();
                _logger.LogInformation(">>> [StartConnectionInternalAsync] SignalR connection started successfully! ConnectionId: {ConnectionId}, Attempt: {Attempt}",
                    _connection.ConnectionId, retryCount + 1);
                return;
            }
            catch (Exception ex) when (retryCount < _options.MaxRetryAttempts)
            {
                retryCount++;
                _logger.LogWarning(ex, "Failed to start SignalR connection on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay} seconds...",
                    retryCount, _options.MaxRetryAttempts + 1, _options.RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR connection after {MaxAttempts} attempts", _options.MaxRetryAttempts + 1);

                // Schedule reconnection
                ScheduleReconnection();
                throw;
            }
        }
    }

    private void ScheduleReconnection()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds));
            if (!_disposed)
            {
                _logger.LogInformation("Attempting scheduled reconnection...");
                try
                {
                    await StartAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed during scheduled reconnection");
                }
            }
        });
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_connection != null)
            {
                if (_isSharedConnection)
                {
                    // This is a shared connection - DO NOT stop or dispose it
                    // The owner (UserManagementClient) will handle lifecycle
                    _logger.LogInformation(">>> [StopAsync] Connection is SHARED - not stopping/disposing (owner will handle)");
                    _connection = null; // Just release our reference
                }
                else
                {
                    // This is our own connection - we can stop and dispose it
                    _logger.LogInformation(">>> [StopAsync] Connection is OWNED - stopping and disposing");

                    try
                    {
                        await _connection.StopAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error stopping SignalR connection");
                    }

                    try
                    {
                        await _connection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing SignalR connection");
                    }

                    _connection = null;
                    _logger.LogInformation("SignalR connection stopped and disposed");
                }
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private void RegisterMethodHandlers()
    {
        if (_connection == null)
        {
            _logger.LogWarning(">>> [RegisterMethodHandlers] Connection is null, cannot register handlers");
            return;
        }

        _logger.LogInformation(">>> [RegisterMethodHandlers] Registering handler for 'InvokeMethod' on connection");

        _connection.On<string, string, object>("InvokeMethod", async (requestId, methodName, parameter) =>
        {
            try
            {
                _logger.LogInformation(">>> [CLIENT RECEIVED] InvokeMethod call: Method={MethodName}, RequestId={RequestId}, Parameter={Parameter}",
                    methodName, requestId, parameter?.GetType().Name ?? "null");

                // ExecuteMethod now returns SignalRResponse directly
                _logger.LogInformation(">>> [CLIENT] Executing method {MethodName}...", methodName);
                var responseDto = await ExecuteMethod(methodName, parameter);

                _logger.LogInformation(">>> [CLIENT] Method {MethodName} executed successfully, ResponseType={ResponseType}",
                    methodName, responseDto.ResponseType);

                // Send response back to the hub with requestId
                _logger.LogInformation(">>> [CLIENT] Sending response back to server for RequestId: {RequestId}", requestId);
                await _connection.InvokeAsync("HandleResponse", requestId, (object?)responseDto);
                _logger.LogInformation(">>> [CLIENT] Response sent successfully for RequestId: {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [CLIENT ERROR] Error executing method {MethodName} for request {RequestId}", methodName, requestId);
                var errorResponse = CreateErrorResponse(ex.Message);

                if (_connection != null)
                {
                    try
                    {
                        _logger.LogInformation(">>> [CLIENT] Sending error response for RequestId: {RequestId}", requestId);
                        await _connection.InvokeAsync("HandleResponse", requestId, errorResponse);
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogError(sendEx, ">>> [CLIENT ERROR] Failed to send error response for method {MethodName} with RequestId: {RequestId}", methodName, requestId);
                    }
                }
            }
        });

        _logger.LogInformation(">>> [RegisterMethodHandlers] Handler registered successfully for 'InvokeMethod'");
    }

    private async Task<SignalRResponse> ExecuteMethod(string methodName, object parameter)
    {
        try
        {
            _logger.LogDebug("Executing method {MethodName} with parameter type {ParameterType}",
                methodName, parameter?.GetType().Name ?? "null");

            // Execute the method using the method executor
            var result = await _methodExecutor.ExecuteAsync(methodName, parameter);

            // Prepare the result for transmission and return as DTO
            return await PrepareResultForTransmission(result, methodName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing method {MethodName}", methodName);
            return CreateErrorResponse(ex.Message);
        }
    }

    private static SignalRResponse CreateErrorResponse(string errorMessage)
    {
        return new SignalRResponse
        {
            ResponseType = SignalRResponseType.Error,
            ErrorMessage = errorMessage
        };
    }

    private async Task<SignalRResponse> PrepareResultForTransmission(object? result, string methodName)
    {
        if (result == null)
        {
            _logger.LogDebug("Method {MethodName} returned null result", methodName);
            return new SignalRResponse
            {
                ResponseType = SignalRResponseType.Null
            };
        }

        try
        {
            // Extract value from ActionResult if present
            var actualResult = ExtractActionResultValue(result);

            // Serialize to check size
            var json = JsonSerializer.Serialize(actualResult, _jsonOptions);
            var dataSize = Utf8Encoding.GetByteCount(json);

            // If data is small enough, send directly through SignalR
            if (dataSize <= _options.MaxDirectDataSizeBytes)
            {
                _logger.LogDebug("Sending {MethodName} result directly via SignalR ({Size} bytes)", methodName, dataSize);
                return new SignalRResponse
                {
                    ResponseType = SignalRResponseType.JsonObject,
                    JsonData = actualResult
                };
            }

            // For large data, upload to server first and return file path
            return await UploadLargeResult(json, methodName, dataSize);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize result for method {MethodName}", methodName);
            return CreateErrorResponse($"Failed to serialize result: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare result for transmission for method {MethodName}", methodName);
            return CreateErrorResponse($"Failed to prepare result: {ex.Message}");
        }
    }

    private static object? ExtractActionResultValue(object result)
    {
        var actualResult = result;
        if (result.GetType().IsGenericType &&
            result.GetType().GetGenericTypeDefinition().FullName == "Microsoft.AspNetCore.Mvc.ActionResult`1")
        {
            var valueProperty = result.GetType().GetProperty("Value");
            if (valueProperty != null)
            {
                actualResult = valueProperty.GetValue(result);
                if (actualResult == null)
                {
                    // If Value is null, try to get Result property (for ObjectResult)
                    var resultProperty = result.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var objectResult = resultProperty.GetValue(result);
                        if (objectResult != null)
                        {
                            var objectResultValueProp = objectResult.GetType().GetProperty("Value");
                            if (objectResultValueProp != null)
                            {
                                actualResult = objectResultValueProp.GetValue(objectResult);
                            }
                        }
                    }
                }
            }
        }

        return actualResult;
    }

    private async Task<SignalRResponse> UploadLargeResult(string json, string methodName, int dataSize)
    {
        _logger.LogInformation("Uploading large {MethodName} result ({Size} bytes) to server", methodName, dataSize);

        try
        {
            var fileName = $"{methodName}_{Guid.NewGuid():N}.json";
            var bytes = Utf8Encoding.GetBytes(json);
            var filePath = await _fileUploadService.UploadFileAsync(bytes, fileName, _options.TempFolder);

            _logger.LogInformation("Successfully uploaded result to: {FilePath}", filePath);
            return new SignalRResponse
            {
                ResponseType = SignalRResponseType.FilePath,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload result for {MethodName}", methodName);
            throw new InvalidOperationException($"Failed to upload large result for method {methodName}: {ex.Message}", ex);
        }
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogWarning(exception, "SignalR connection closed. Will attempt to reconnect in {DelaySeconds} seconds...",
            _options.ReconnectDelaySeconds);

        // Wait configured delay before attempting to reconnect
        await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds));

        if (!_disposed)
        {
            try
            {
                _logger.LogInformation("Attempting to reconnect after connection closed...");
                await StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect after connection closed");
            }
        }
    }

    /// <summary>
    /// Disposes the client and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;

            try
            {
                // Use the async StopAsync method which handles cleanup properly
                // StopAsync already checks _isSharedConnection flag
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ">>> [Dispose] Error stopping SignalR connection during disposal");
            }

            try
            {
                _connectionSemaphore?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ">>> [Dispose] Error disposing connection semaphore");
            }

            _logger.LogInformation(">>> [Dispose] SignalRCommunicatorClient disposed (SharedConnection: {IsShared})", _isSharedConnection);
        }
    }
}
