using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nesco.SignalRCommunicator.Client.Services;

/// <summary>
/// Background service that manages the lifecycle of the SignalR client connection.
/// Starts the connection on application startup and stops it on shutdown.
/// </summary>
public class SignalRClientHostedService : IHostedService
{
    private readonly ISignalRCommunicatorClient _signalRClient;
    private readonly ILogger<SignalRClientHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRClientHostedService"/> class.
    /// </summary>
    public SignalRClientHostedService(
        ISignalRCommunicatorClient signalRClient,
        ILogger<SignalRClientHostedService> logger)
    {
        _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the SignalR connection in the background when the application starts.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing SignalR background connection to server...");

        // Start the connection in the background without awaiting
        _ = Task.Run(async () =>
        {
            try
            {
                await _signalRClient.StartAsync();
                _logger.LogInformation("SignalR connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish SignalR connection on startup");
                // Don't throw - let the service continue running and rely on reconnect logic
            }
        }, cancellationToken);

        // Return completed task immediately to allow app startup to continue
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the SignalR connection when the application shuts down.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SignalR connection...");
        try
        {
            await _signalRClient.StopAsync();
            _logger.LogInformation("SignalR connection stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SignalR connection");
        }
    }
}
