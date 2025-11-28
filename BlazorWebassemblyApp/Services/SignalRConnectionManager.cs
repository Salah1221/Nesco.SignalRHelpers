using Nesco.SignalRUserManagement.Client.Services;
using Nesco.SignalRCommunicator.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorWebassemblyApp.Services;

public class SignalRConnectionManager
{
    private readonly AuthenticationService _authService;
    private readonly UserConnectionClient _userManagementClient;
    private readonly ISignalRCommunicatorClient _communicatorClient;
    private readonly ServerConfiguration _serverConfig;
    private readonly ILogger<SignalRConnectionManager> _logger;

    public SignalRConnectionManager(
        AuthenticationService authService,
        UserConnectionClient userManagementClient,
        ISignalRCommunicatorClient communicatorClient,
        ServerConfiguration serverConfig,
        ILogger<SignalRConnectionManager> logger)
    {
        _authService = authService;
        _userManagementClient = userManagementClient;
        _communicatorClient = communicatorClient;
        _serverConfig = serverConfig;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync()
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Cannot connect to SignalR: No auth token available");
            return false;
        }

        try
        {
            // Connect to UserManagement Hub - Communicator will share this connection!
            var userManagementUrl = $"{_serverConfig.ServerUrl}/hubs/usermanagement";
            _logger.LogInformation(">>> [ConnectAsync] Connecting to UserManagement hub at {Url}", userManagementUrl);

            // Initialize UserManagement client - this creates the connection
            await _userManagementClient.InitializeAsync(
                userManagementUrl,
                async () => await _authService.GetTokenAsync() ?? string.Empty
            );

            _logger.LogInformation(">>> [ConnectAsync] UserManagement connected. ConnectionId: {ConnectionId}",
                _userManagementClient.ConnectionId);

            // Share the same connection with Communicator client - NO NEW CONNECTION!
            _logger.LogInformation(">>> [ConnectAsync] Sharing connection with Communicator client...");
            if (_communicatorClient is SignalRCommunicatorClient communicatorClient)
            {
                if (_userManagementClient.HubConnection != null)
                {
                    // Use the SAME connection instead of creating a new one
                    _logger.LogInformation(">>> [ConnectAsync] About to share connection. HubConnection.ConnectionId: {ConnectionId}, State: {State}",
                        _userManagementClient.HubConnection.ConnectionId, _userManagementClient.HubConnection.State);

                    communicatorClient.UseExistingConnection(_userManagementClient.HubConnection);

                    _logger.LogInformation(">>> [ConnectAsync] Communicator now listening on SHARED connection: {ConnectionId}. This connection is registered in the database!",
                        _userManagementClient.ConnectionId);
                }
                else
                {
                    _logger.LogError(">>> [ConnectAsync] UserManagement HubConnection is null!");
                    return false;
                }
            }
            else
            {
                _logger.LogWarning(">>> [ConnectAsync] Communicator client is not SignalRCommunicatorClient, falling back to separate connection");
                await _communicatorClient.StartAsync();
            }

            // Verify connection
            var isConnected = _communicatorClient.IsConnected;
            _logger.LogInformation(">>> [ConnectAsync] Connection shared successfully! IsConnected: {IsConnected}, ConnectionId: {ConnectionId}",
                isConnected, _userManagementClient.ConnectionId);

            _logger.LogInformation(">>> [ConnectAsync] SignalR connection established (SINGLE shared connection)");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hubs");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _logger.LogInformation(">>> [DisconnectAsync] Disconnecting from SignalR...");

            // Only stop the UserManagement client since Communicator is sharing the same connection
            await _userManagementClient.StopAsync();

            // Don't call StopAsync on CommunicatorClient since it's using the same connection
            // await _communicatorClient.StopAsync(); // This would double-stop the same connection

            _logger.LogInformation(">>> [DisconnectAsync] Disconnected from SignalR hubs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [DisconnectAsync ERROR] Error disconnecting from SignalR hubs");
        }
    }
}
