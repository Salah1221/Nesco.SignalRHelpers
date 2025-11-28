using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Options;
using Nesco.SignalRCommunicator.Core.Services;
using Nesco.SignalRCommunicator.Server.Hubs;
using Nesco.SignalRCommunicator.Server.Services;

namespace Nesco.SignalRCommunicator.Server.Extensions;

/// <summary>
/// Extension methods for configuring SignalR Communicator Server services.
/// </summary>
public static class SignalRServerExtensions
{
    /// <summary>
    /// Adds SignalR Communicator Server services to the service collection with the default file reader service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorServer(options =>
    /// {
    ///     options.MaxConcurrentRequests = 20;
    ///     options.RequestTimeoutSeconds = 600; // 10 minutes
    ///     options.AutoDeleteTempFiles = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorServer(
        this IServiceCollection services,
        Action<SignalRServerOptions>? configureOptions = null)
    {
        return AddSignalRCommunicatorServer<DefaultFileReaderService>(services, configureOptions);
    }

    /// <summary>
    /// Adds SignalR Communicator Server services to the service collection with a custom file reader service.
    /// </summary>
    /// <typeparam name="TFileReaderService">The type of the custom file reader service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorServer&lt;MyCustomFileReaderService&gt;(options =>
    /// {
    ///     options.MaxConcurrentRequests = 20;
    ///     options.RequestTimeoutSeconds = 600;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorServer<TFileReaderService>(
        this IServiceCollection services,
        Action<SignalRServerOptions>? configureOptions = null)
        where TFileReaderService : class, IFileReaderService
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            // Register default options
            services.Configure<SignalRServerOptions>(_ => { });
        }

        // Register SignalR
        services.AddSignalR();

        // Register response manager (singleton to manage pending requests across all services)
        services.AddSingleton<IResponseManager, ResponseManager>();

        // Register file reader service
        if (typeof(TFileReaderService) == typeof(DefaultFileReaderService))
        {
            services.AddSingleton<IFileReaderService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DefaultFileReaderService>>();
                return new DefaultFileReaderService(logger);
            });
        }
        else
        {
            services.AddSingleton<IFileReaderService, TFileReaderService>();
        }

        // Register the communicator service
        services.AddSingleton<ISignalRCommunicatorService, SignalRCommunicatorService>();

        return services;
    }

    /// <summary>
    /// Adds SignalR Communicator Server services with a custom web root path for file reading.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="webRootPath">The custom web root path where files are stored.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorServer("/custom/path/wwwroot", options =>
    /// {
    ///     options.MaxConcurrentRequests = 15;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorServer(
        this IServiceCollection services,
        string webRootPath,
        Action<SignalRServerOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<SignalRServerOptions>(_ => { });
        }

        // Register SignalR
        services.AddSignalR();

        // Register response manager (singleton to manage pending requests across all services)
        services.AddSingleton<IResponseManager, ResponseManager>();

        // Register file reader service with custom web root
        services.AddSingleton<IFileReaderService>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DefaultFileReaderService>>();
            return new DefaultFileReaderService(logger, webRootPath);
        });

        // Register the communicator service
        services.AddSingleton<ISignalRCommunicatorService, SignalRCommunicatorService>();

        return services;
    }

    /// <summary>
    /// Adds SignalR Communicator Server services using an existing hub instead of creating its own.
    /// This allows sharing connection IDs between multiple services.
    /// </summary>
    /// <typeparam name="THub">The existing hub type to use for communication.</typeparam>
    /// <typeparam name="TFileReaderService">The type of the file reader service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // Use UserManagementHub for SignalR Communicator
    /// services.AddSignalRCommunicatorServerWithHub&lt;UserManagementHub&lt;MyDbContext&gt;&gt;(options =>
    /// {
    ///     options.MaxConcurrentRequests = 20;
    ///     options.RequestTimeoutSeconds = 60;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorServerWithHub<THub>(
        this IServiceCollection services,
        Action<SignalRServerOptions>? configureOptions = null)
        where THub : Hub
    {
        return AddSignalRCommunicatorServerWithHub<THub, DefaultFileReaderService>(services, configureOptions);
    }

    /// <summary>
    /// Adds SignalR Communicator Server services using an existing hub with a custom file reader service.
    /// </summary>
    /// <typeparam name="THub">The existing hub type to use for communication.</typeparam>
    /// <typeparam name="TFileReaderService">The type of the custom file reader service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSignalRCommunicatorServerWithHub<THub, TFileReaderService>(
        this IServiceCollection services,
        Action<SignalRServerOptions>? configureOptions = null)
        where THub : Hub
        where TFileReaderService : class, IFileReaderService
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<SignalRServerOptions>(_ => { });
        }

        // Note: SignalR and the hub should already be registered by the other service

        // Register response manager (singleton to manage pending requests across all services)
        services.AddSingleton<IResponseManager, ResponseManager>();

        // Register file reader service
        if (typeof(TFileReaderService) == typeof(DefaultFileReaderService))
        {
            services.AddSingleton<IFileReaderService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DefaultFileReaderService>>();
                return new DefaultFileReaderService(logger);
            });
        }
        else
        {
            services.AddSingleton<IFileReaderService, TFileReaderService>();
        }

        // Register the communicator service with the specified hub type
        services.AddSingleton<ISignalRCommunicatorService, SignalRCommunicatorService<THub>>();

        return services;
    }

    /// <summary>
    /// Maps the SignalR Communicator Hub to the specified path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The hub path pattern. Defaults to "/hubs/communicator".</param>
    /// <returns>The hub endpoint convention builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// app.MapHub&lt;SignalRCommunicatorHub&gt;("/hubs/mycommunicator");
    ///
    /// // Or using the extension method:
    /// app.UseEndpoints(endpoints =>
    /// {
    ///     endpoints.MapSignalRCommunicatorHub("/hubs/mycommunicator");
    /// });
    /// </code>
    /// </example>
    public static HubEndpointConventionBuilder MapSignalRCommunicatorHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/hubs/communicator")
    {
        return endpoints.MapHub<SignalRCommunicatorHub>(pattern);
    }
}
