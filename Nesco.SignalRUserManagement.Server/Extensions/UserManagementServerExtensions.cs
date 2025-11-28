using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRUserManagement.Core.Interfaces;
using Nesco.SignalRUserManagement.Core.Options;
using Nesco.SignalRUserManagement.Server.Hubs;
using Nesco.SignalRUserManagement.Server.Services;

namespace Nesco.SignalRUserManagement.Server.Extensions;

/// <summary>
/// Extension methods for configuring SignalR User Management on the server
/// </summary>
public static class UserManagementServerExtensions
{
    /// <summary>
    /// Adds SignalR User Management services to the service collection
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext containing ConnectedUsers and Connections DbSets</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional configuration for user management options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUserManagement<TDbContext>(
        this IServiceCollection services,
        Action<UserManagementOptions>? configureOptions = null)
        where TDbContext : DbContext
    {
        // Configure options
        var options = new UserManagementOptions();
        configureOptions?.Invoke(options);
        services.Configure<UserManagementOptions>(opts =>
        {
            opts.BroadcastConnectionEvents = options.BroadcastConnectionEvents;
            opts.ConnectionEventMethod = options.ConnectionEventMethod;
            opts.AutoPurgeOfflineConnections = options.AutoPurgeOfflineConnections;
            opts.KeepAliveIntervalSeconds = options.KeepAliveIntervalSeconds;
            opts.ClientTimeoutSeconds = options.ClientTimeoutSeconds;
            opts.TrackUserAgent = options.TrackUserAgent;
            opts.OnUserConnected = options.OnUserConnected;
            opts.OnUserDisconnected = options.OnUserDisconnected;
        });

        // Register SignalR with options
        services.AddSignalR(signalrOptions =>
        {
            signalrOptions.KeepAliveInterval = TimeSpan.FromSeconds(options.KeepAliveIntervalSeconds);
            signalrOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(options.ClientTimeoutSeconds);
            signalrOptions.HandshakeTimeout = TimeSpan.FromSeconds(15);
        });

        // Register the user connection service using UserManagementHub
        services.AddScoped<IUserConnectionService, UserConnectionService<UserManagementHub<TDbContext>, TDbContext>>();

        return services;
    }

    /// <summary>
    /// Maps the UserManagementHub to the specified path
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type</typeparam>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="pattern">The hub path pattern</param>
    /// <returns>The hub endpoint convention builder</returns>
    public static HubEndpointConventionBuilder MapUserManagementHub<TDbContext>(
        this IEndpointRouteBuilder endpoints,
        string pattern) where TDbContext : DbContext
    {
        return endpoints.MapHub<UserManagementHub<TDbContext>>(pattern);
    }
}
