using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BlazorServerApplication.Components;
using BlazorServerApplication.Components.Account;
using BlazorServerApplication.Data;
using BlazorServerApplication.Services;
using Nesco.SignalRUserManagement.Server.Extensions;
using Nesco.SignalRCommunicator.Server.Extensions;
using Nesco.SignalRCommunicator.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Configure authentication with both Cookie (for Blazor Server) and JWT Bearer (for API and SignalR)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Add JWT Bearer Authentication for API endpoints and SignalR
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "DemoSecretKey1234567890ABCDEFGHIJKLMNOP"; // Minimum 32 characters
var issuer = jwtSettings["Issuer"] ?? "BlazorServerApplication";
var audience = jwtSettings["Audience"] ?? "BlazorWebassemblyApp";

// Add JWT Bearer to existing authentication configuration
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        // Configure JWT authentication for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // If the request is for our hubs...
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/usermanagement") ||
                     path.StartsWithSegments("/hubs/communicator")))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add API Controllers
builder.Services.AddControllers();

// Add custom SignalR UserIdProvider for JWT authentication
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// Add SignalR User Management
builder.Services.AddUserManagement<ApplicationDbContext>(options =>
{
    options.BroadcastConnectionEvents = true;
    options.ConnectionEventMethod = "UserConnectionEvent";
    options.AutoPurgeOfflineConnections = true;
    options.KeepAliveIntervalSeconds = 15;
    options.ClientTimeoutSeconds = 30;
    options.TrackUserAgent = true;
});

// Add SignalR Communicator - sharing UserManagementHub to have same connection IDs
builder.Services.AddSignalRCommunicatorServerWithHub<Nesco.SignalRUserManagement.Server.Hubs.UserManagementHub<ApplicationDbContext>>(options =>
{
    options.MaxConcurrentRequests = 10;
    options.RequestTimeoutSeconds = 60;
    options.SemaphoreTimeoutSeconds = 5;
    options.AutoDeleteTempFiles = true;
    options.TempFolder = "signalr-temp";
});

// Add Unified SignalR Service (coordination layer)
builder.Services.AddScoped<IUnifiedSignalRService, UnifiedSignalRService>();

// Add SignalR Dashboard (reusable NuGet package component)
builder.Services.AddSignalRDashboard<ApplicationUser, ApplicationDbContext>();

// Add CORS for Blazor WASM client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "https://localhost:7059",
                "http://localhost:7059",
                "https://localhost:5274",
                "http://localhost:5274")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Comment out HTTPS redirection for HTTP demo
// app.UseHttpsRedirection();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Nesco.SignalRCommunicator.Dashboard.ServiceCollectionExtensions).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Map API Controllers
app.MapControllers();

// Map SignalR hubs - only need UserManagement hub now (Communicator shares it)
app.MapUserManagementHub<ApplicationDbContext>("/hubs/usermanagement");
// Note: SignalRCommunicator now shares the UserManagement hub, so no separate hub mapping needed

app.Run();