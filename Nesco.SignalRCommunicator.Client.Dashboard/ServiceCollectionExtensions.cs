using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRCommunicator.Client.Dashboard.Services;

namespace Nesco.SignalRCommunicator.Client.Dashboard;

/// <summary>
/// Extension methods for registering SignalR Client Dashboard services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SignalR Client Dashboard services to the service collection.
    /// This includes the method invocation logger and client method registry.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSignalRClientDashboard(this IServiceCollection services)
    {
        // Register singleton services (persist across component lifecycle)
        services.AddSingleton<IMethodInvocationLogger, MethodInvocationLogger>();
        services.AddSingleton<IClientMethodRegistry, ClientMethodRegistry>();

        return services;
    }

    /// <summary>
    /// Adds the SignalR Client Dashboard services with pre-registered methods.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureRegistry">Action to configure the method registry</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSignalRClientDashboard(
        this IServiceCollection services,
        Action<IClientMethodRegistry> configureRegistry)
    {
        // Register services first
        services.AddSignalRClientDashboard();

        // Build service provider to get the registry and configure it
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IClientMethodRegistry>();
        configureRegistry(registry);

        return services;
    }
}
