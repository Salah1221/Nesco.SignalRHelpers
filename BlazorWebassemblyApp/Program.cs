using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorWebassemblyApp;
using BlazorWebassemblyApp.Services;
using Nesco.SignalRUserManagement.Client.Services;
using Nesco.SignalRCommunicator.Client.Services;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Options;
using Nesco.SignalRUserManagement.Core.Options;
using Nesco.SignalRCommunicator.Client.Dashboard;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add Authentication Services
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<SignalRConnectionManager>();

// Add Method Invocation Logger (singleton to persist across components)
builder.Services.AddSingleton<MethodInvocationLogger>();

// Add SignalR Client Dashboard services (from package)
builder.Services.AddSignalRClientDashboard();

// Get server URL (default to localhost:5200 for HTTP demo)
var serverUrl = builder.Configuration["ServerUrl"] ?? "http://localhost:5200";

// Note: We'll configure SignalR to connect manually after login
// For now, register services without auto-connect
builder.Services.AddSingleton<UserConnectionClient>();
builder.Services.AddSingleton<ISignalRCommunicatorClient, SignalRCommunicatorClient>();
builder.Services.AddSingleton<IMethodExecutor, ClientMethodsService>();

// Configure SignalR client options - using UserManagement hub (shared with Communicator)
builder.Services.Configure<SignalRClientOptions>(options =>
{
    options.ServerUrl = serverUrl;
    options.HubPath = "/hubs/usermanagement"; // Communicator now shares UserManagement hub
    options.MaxRetryAttempts = 3;
});

builder.Services.Configure<UserManagementOptions>(options =>
{
    options.BroadcastConnectionEvents = true;
    options.ConnectionEventMethod = "UserConnectionEvent";
    options.AutoReconnect = true;
    options.AutoReconnectRetryDelaysSeconds = new[] { 0, 2, 5, 10, 30 }; // Progressive backoff
});

// Add HttpClient for file upload service
builder.Services.AddHttpClient<IFileUploadService, DefaultFileUploadService>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Store server URL for later use
builder.Services.AddSingleton(new ServerConfiguration { ServerUrl = serverUrl });

await builder.Build().RunAsync();

// Simple configuration class for server URL
public class ServerConfiguration
{
    public string ServerUrl { get; set; } = string.Empty;
}