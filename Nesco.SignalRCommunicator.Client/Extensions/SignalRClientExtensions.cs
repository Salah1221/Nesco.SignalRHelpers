using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRCommunicator.Client.Services;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Options;

namespace Nesco.SignalRCommunicator.Client.Extensions;

/// <summary>
/// Extension methods for configuring SignalR Communicator Client services.
/// </summary>
public static class SignalRClientExtensions
{
    /// <summary>
    /// Adds SignalR Communicator Client services to the service collection with the default HTTP-based file upload service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorClient(options =>
    /// {
    ///     options.ServerUrl = "https://myserver.com";
    ///     options.HubPath = "/hubs/mycommunicator";
    ///     options.MaxDirectDataSizeBytes = 20 * 1024; // 20KB
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorClient(
        this IServiceCollection services,
        Action<SignalRClientOptions> configureOptions)
    {
        return AddSignalRCommunicatorClient<DefaultFileUploadService>(services, configureOptions);
    }

    /// <summary>
    /// Adds SignalR Communicator Client services to the service collection with a custom file upload service.
    /// </summary>
    /// <typeparam name="TFileUploadService">The type of the custom file upload service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorClient&lt;MyCustomFileUploadService&gt;(options =>
    /// {
    ///     options.ServerUrl = "https://myserver.com";
    ///     options.HubPath = "/hubs/mycommunicator";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorClient<TFileUploadService>(
        this IServiceCollection services,
        Action<SignalRClientOptions> configureOptions)
        where TFileUploadService : class, IFileUploadService
    {
        // Configure options
        services.Configure(configureOptions);

        // Get options to configure HttpClient
        var options = new SignalRClientOptions();
        configureOptions(options);

        // Register HttpClient for file uploads (if using DefaultFileUploadService)
        if (typeof(TFileUploadService) == typeof(DefaultFileUploadService))
        {
            services.AddHttpClient<IFileUploadService, DefaultFileUploadService>(client =>
            {
                client.BaseAddress = new Uri(options.ServerUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        }
        else
        {
            // Register custom file upload service
            services.AddScoped<IFileUploadService, TFileUploadService>();
        }

        // Register core services
        services.AddSingleton<ISignalRCommunicatorClient, SignalRCommunicatorClient>();
        services.AddHostedService<SignalRClientHostedService>();

        return services;
    }

    /// <summary>
    /// Adds SignalR Communicator Client services with a custom method executor.
    /// This is the recommended method when you want to provide your own method routing logic.
    /// </summary>
    /// <typeparam name="TMethodExecutor">The type of the method executor implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorClientWithExecutor&lt;MyMethodExecutor&gt;(options =>
    /// {
    ///     options.ServerUrl = "https://myserver.com";
    ///     options.HubPath = "/hubs/mycommunicator";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorClientWithExecutor<TMethodExecutor>(
        this IServiceCollection services,
        Action<SignalRClientOptions> configureOptions)
        where TMethodExecutor : class, IMethodExecutor
    {
        // Add the client with default file upload service
        AddSignalRCommunicatorClient(services, configureOptions);

        // Register custom method executor
        services.AddSingleton<IMethodExecutor, TMethodExecutor>();

        return services;
    }

    /// <summary>
    /// Adds SignalR Communicator Client services with custom method executor and file upload service.
    /// </summary>
    /// <typeparam name="TMethodExecutor">The type of the method executor implementation.</typeparam>
    /// <typeparam name="TFileUploadService">The type of the file upload service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="SignalRClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSignalRCommunicatorClientWithExecutor&lt;MyMethodExecutor, MyFileUploadService&gt;(options =>
    /// {
    ///     options.ServerUrl = "https://myserver.com";
    ///     options.HubPath = "/hubs/mycommunicator";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSignalRCommunicatorClientWithExecutor<TMethodExecutor, TFileUploadService>(
        this IServiceCollection services,
        Action<SignalRClientOptions> configureOptions)
        where TMethodExecutor : class, IMethodExecutor
        where TFileUploadService : class, IFileUploadService
    {
        // Add the client with custom file upload service
        AddSignalRCommunicatorClient<TFileUploadService>(services, configureOptions);

        // Register custom method executor
        services.AddSingleton<IMethodExecutor, TMethodExecutor>();

        return services;
    }
}
