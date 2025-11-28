using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nesco.SignalRUserManagement.Client.Services;

/// <summary>
/// Background service that ensures the UserConnectionClient is started and maintained
/// </summary>
public class UserConnectionHostedService : IHostedService
{
    private readonly UserConnectionClient _connectionClient;
    private readonly ILogger<UserConnectionHostedService> _logger;

    public UserConnectionHostedService(
        UserConnectionClient connectionClient,
        ILogger<UserConnectionHostedService> logger)
    {
        _connectionClient = connectionClient ?? throw new ArgumentNullException(nameof(connectionClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserConnection hosted service starting");

        // Note: The actual connection is started when InitializeAsync is called
        // This hosted service just ensures the service lifetime is managed

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserConnection hosted service stopping");
        await _connectionClient.StopAsync();
    }
}
