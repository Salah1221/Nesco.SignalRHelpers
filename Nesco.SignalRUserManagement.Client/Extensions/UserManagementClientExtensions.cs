using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRUserManagement.Client.Services;
using Nesco.SignalRUserManagement.Core.Options;

namespace Nesco.SignalRUserManagement.Client.Extensions;

/// <summary>
/// Extension methods for configuring SignalR User Management on the client
/// </summary>
public static class UserManagementClientExtensions
{
    /// <summary>
    /// Adds SignalR User Management client services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional configuration for user management options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUserManagementClient(
        this IServiceCollection services,
        Action<UserManagementOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<UserManagementOptions>(_ => { });
        }

        // Register the connection client as singleton (shared across the application)
        services.AddSingleton<UserConnectionClient>();

        // Register the hosted service to manage connection lifecycle
        services.AddHostedService<UserConnectionHostedService>();

        return services;
    }

    /// <summary>
    /// Adds SignalR User Management client services with a custom initialization action
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="hubUrl">The hub URL to connect to</param>
    /// <param name="accessTokenProvider">Optional function to provide access token</param>
    /// <param name="configureOptions">Optional configuration for user management options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddUserManagementClient(
        this IServiceCollection services,
        string hubUrl,
        Func<Task<string>>? accessTokenProvider = null,
        Action<UserManagementOptions>? configureOptions = null)
    {
        // Add base services
        services.AddUserManagementClient(configureOptions);

        // Store initialization parameters for later use
        services.AddSingleton(new UserConnectionInitializationOptions
        {
            HubUrl = hubUrl,
            AccessTokenProvider = accessTokenProvider
        });

        return services;
    }
}

/// <summary>
/// Options for initializing the user connection
/// </summary>
public class UserConnectionInitializationOptions
{
    public string HubUrl { get; set; } = string.Empty;
    public Func<Task<string>>? AccessTokenProvider { get; set; }
}
