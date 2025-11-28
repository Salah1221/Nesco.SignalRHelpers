using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nesco.SignalRUserManagement.Core.Models;
using Nesco.SignalRUserManagement.Core.Options;

namespace Nesco.SignalRUserManagement.Client.Services;

/// <summary>
/// Client service for managing SignalR user connections
/// </summary>
public class UserConnectionClient : IAsyncDisposable
{
    private readonly ILogger<UserConnectionClient> _logger;
    private readonly UserManagementOptions _options;
    private HubConnection? _hubConnection;
    private Func<Task<string>>? _accessTokenProvider;

    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    public event Action<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Event raised when the connection starts reconnecting
    /// </summary>
    public event Action? Reconnecting;

    /// <summary>
    /// Event raised when the connection successfully reconnects
    /// </summary>
    public event Action<string?>? Reconnected;

    /// <summary>
    /// Event raised when a message is received from the hub
    /// </summary>
    public event Func<string, object?, Task>? MessageReceived;

    /// <summary>
    /// Event raised when a user connection event is received
    /// </summary>
    public event Func<UserConnectionEventArgs, Task>? UserConnectionEventReceived;

    /// <summary>
    /// Gets whether the client is currently connected
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Gets the current connection state
    /// </summary>
    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Gets the current connection ID (null if not connected)
    /// </summary>
    public string? ConnectionId => _hubConnection?.ConnectionId;

    /// <summary>
    /// Gets the underlying HubConnection (for sharing with other services)
    /// </summary>
    public HubConnection? HubConnection => _hubConnection;

    public UserConnectionClient(
        ILogger<UserConnectionClient> logger,
        IOptions<UserManagementOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Initializes the connection to the hub
    /// </summary>
    /// <param name="hubUrl">Full URL to the hub endpoint</param>
    /// <param name="accessTokenProvider">Optional function to provide access token for authentication</param>
    public async Task InitializeAsync(string hubUrl, Func<Task<string>>? accessTokenProvider = null)
    {
        if (_hubConnection != null)
        {
            _logger.LogWarning("Hub connection already initialized");
            return;
        }

        _logger.LogInformation("Initializing hub connection to {HubUrl}", hubUrl);
        _accessTokenProvider = accessTokenProvider;

        var builder = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (accessTokenProvider != null)
                {
                    options.AccessTokenProvider = async () => await accessTokenProvider();
                }
            });

        // Add automatic reconnect if enabled in options
        _logger.LogInformation(">>> [InitializeAsync] AutoReconnect setting: {AutoReconnect}", _options.AutoReconnect);

        if (_options.AutoReconnect)
        {
            var retryDelays = _options.AutoReconnectRetryDelaysSeconds
                .Select(seconds => TimeSpan.FromSeconds(seconds))
                .ToArray();

            _logger.LogInformation(">>> [InitializeAsync] Applying WithAutomaticReconnect with delays: [{Delays}]",
                string.Join(", ", _options.AutoReconnectRetryDelaysSeconds.Select(d => $"{d}s")));

            builder.WithAutomaticReconnect(retryDelays);

            _logger.LogInformation(">>> [InitializeAsync] WithAutomaticReconnect applied successfully");
        }
        else
        {
            _logger.LogWarning(">>> [InitializeAsync] Auto-reconnect is DISABLED in options");
        }

        _hubConnection = builder.Build();

        _logger.LogInformation(">>> [InitializeAsync] HubConnection built successfully");

        // Register connection state handlers
        _hubConnection.Closed += OnConnectionClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        // Register for user connection events if enabled
        if (_options.BroadcastConnectionEvents)
        {
            _hubConnection.On<UserConnectionEventArgs>(_options.ConnectionEventMethod, async (eventArgs) =>
            {
                _logger.LogDebug("Received user connection event: {EventType} for user {UserId}",
                    eventArgs.EventType, eventArgs.UserId);

                if (UserConnectionEventReceived != null)
                {
                    await UserConnectionEventReceived.Invoke(eventArgs);
                }
            });
        }

        await StartConnectionAsync();
    }

    /// <summary>
    /// Registers a handler for a specific message type
    /// </summary>
    /// <param name="methodName">The method name to listen for</param>
    /// <param name="handler">The handler to invoke when the message is received</param>
    public void On<T>(string methodName, Func<T, Task> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");
        }

        _hubConnection.On(methodName, handler);
        _logger.LogDebug("Registered handler for method: {MethodName}", methodName);
    }

    /// <summary>
    /// Registers a handler for a specific message type (synchronous)
    /// </summary>
    public void On<T>(string methodName, Action<T> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");
        }

        _hubConnection.On(methodName, handler);
        _logger.LogDebug("Registered handler for method: {MethodName}", methodName);
    }

    /// <summary>
    /// Invokes a hub method on the server
    /// </summary>
    public async Task SendAsync(string methodName, object? arg = null)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");
        }

        if (_hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException($"Cannot send message. Connection state: {_hubConnection.State}");
        }

        await _hubConnection.SendAsync(methodName, arg);
        _logger.LogDebug("Sent message to hub: {MethodName}", methodName);
    }

    /// <summary>
    /// Invokes a hub method on the server and waits for a result
    /// </summary>
    public async Task<TResult> InvokeAsync<TResult>(string methodName, object? arg = null)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");
        }

        if (_hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException($"Cannot invoke method. Connection state: {_hubConnection.State}");
        }

        var result = await _hubConnection.InvokeAsync<TResult>(methodName, arg);
        _logger.LogDebug("Invoked hub method: {MethodName}", methodName);
        return result;
    }

    /// <summary>
    /// Manually reconnects to the hub
    /// </summary>
    public async Task ReconnectAsync()
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");
        }

        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            _logger.LogInformation("Manually reconnecting to hub");
            await StartConnectionAsync();
        }
        else
        {
            _logger.LogWarning("Cannot reconnect. Current state: {State}", _hubConnection.State);
        }
    }

    /// <summary>
    /// Stops the connection to the hub
    /// </summary>
    public async Task StopAsync()
    {
        if (_hubConnection == null) return;

        _logger.LogInformation("Stopping hub connection");
        await _hubConnection.StopAsync();
        ConnectionStatusChanged?.Invoke(false);
    }

    private async Task StartConnectionAsync()
    {
        if (_hubConnection == null) return;

        // If AutoReconnect is enabled, retry initial connection with same delays
        if (_options.AutoReconnect)
        {
            var retryDelays = _options.AutoReconnectRetryDelaysSeconds;
            var attempt = 0;
            var maxAttempts = retryDelays.Length;

            while (attempt <= maxAttempts)
            {
                try
                {
                    _logger.LogInformation(">>> [StartConnectionAsync] Attempt {Attempt}/{MaxAttempts} to start connection...",
                        attempt + 1, maxAttempts + 1);

                    await _hubConnection.StartAsync();

                    _logger.LogInformation(">>> [StartConnectionAsync] Hub connection started successfully! ConnectionId: {ConnectionId}",
                        _hubConnection.ConnectionId);
                    ConnectionStatusChanged?.Invoke(true);
                    return; // Success!
                }
                catch (Exception ex)
                {
                    if (attempt < maxAttempts)
                    {
                        var delaySeconds = retryDelays[attempt];
                        _logger.LogWarning(ex, ">>> [StartConnectionAsync] Connection attempt {Attempt} failed. Retrying in {Delay}s...",
                            attempt + 1, delaySeconds);

                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        attempt++;
                    }
                    else
                    {
                        _logger.LogError(ex, ">>> [StartConnectionAsync] All {MaxAttempts} connection attempts failed",
                            maxAttempts + 1);
                        ConnectionStatusChanged?.Invoke(false);
                        throw;
                    }
                }
            }
        }
        else
        {
            // AutoReconnect disabled - single attempt only
            try
            {
                _logger.LogInformation(">>> [StartConnectionAsync] Starting connection (AutoReconnect disabled - single attempt)...");
                await _hubConnection.StartAsync();
                _logger.LogInformation("Hub connection started successfully. ConnectionId: {ConnectionId}",
                    _hubConnection.ConnectionId);
                ConnectionStatusChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [StartConnectionAsync] Error starting hub connection");
                ConnectionStatusChanged?.Invoke(false);
                throw;
            }
        }
    }

    private Task OnConnectionClosed(Exception? error)
    {
        _logger.LogWarning(">>> [OnConnectionClosed] Hub connection closed! Error: {Error}, State: {State}",
            error?.Message ?? "No error", _hubConnection?.State.ToString() ?? "Unknown");

        if (_options.AutoReconnect)
        {
            _logger.LogInformation(">>> [OnConnectionClosed] AutoReconnect is enabled - SignalR should automatically attempt reconnection");
        }
        else
        {
            _logger.LogWarning(">>> [OnConnectionClosed] AutoReconnect is DISABLED - no automatic reconnection will occur");
        }

        ConnectionStatusChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? error)
    {
        _logger.LogInformation(">>> [OnReconnecting] Hub connection is RECONNECTING! Error: {Error}", error?.Message ?? "No error");
        ConnectionStatusChanged?.Invoke(false);
        Reconnecting?.Invoke();
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation(">>> [OnReconnected] Hub connection SUCCESSFULLY reconnected! New ConnectionId: {ConnectionId}", connectionId);
        ConnectionStatusChanged?.Invoke(true);
        Reconnected?.Invoke(connectionId);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            _logger.LogInformation("Disposing hub connection");
            await _hubConnection.DisposeAsync();
        }
    }
}
