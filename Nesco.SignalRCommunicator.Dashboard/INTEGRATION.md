# Integration Guide: Nesco.SignalRCommunicator.Dashboard

This guide shows how to integrate the SignalR Dashboard package into your Blazor Server application.

## Prerequisites

Your project must already have:
- `Nesco.SignalRUserManagement.Server`
- `Nesco.SignalRCommunicator.Server`
- ASP.NET Core Identity configured
- Entity Framework Core with a DbContext

## Step-by-Step Integration

### 1. Build the NuGet Package

From the solution root:

```bash
dotnet build Nesco.SignalRCommunicator.Dashboard/Nesco.SignalRCommunicator.Dashboard.csproj
dotnet pack Nesco.SignalRCommunicator.Dashboard/Nesco.SignalRCommunicator.Dashboard.csproj -c Release
```

The package will be created in: `Nesco.SignalRCommunicator.Dashboard/bin/Release/Nesco.SignalRCommunicator.Dashboard.1.0.0.nupkg`

### 2. Reference the Package

#### Option A: Project Reference (Development)

In your Blazor Server application's `.csproj`:

```xml
<ItemGroup>
    <ProjectReference Include="..\Nesco.SignalRCommunicator.Dashboard\Nesco.SignalRCommunicator.Dashboard.csproj" />
</ItemGroup>
```

#### Option B: NuGet Package (Production)

```bash
dotnet add package Nesco.SignalRCommunicator.Dashboard --version 1.0.0
```

Or add to `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="Nesco.SignalRCommunicator.Dashboard" Version="1.0.0" />
</ItemGroup>
```

### 3. Register Services in Program.cs

Add the dashboard service registration to your `Program.cs`:

```csharp
using Nesco.SignalRCommunicator.Dashboard;
using BlazorServerApplication.Data; // Your ApplicationUser namespace

var builder = WebApplication.CreateBuilder(args);

// ... existing services ...

// Ensure SignalR User Management is registered
builder.Services.AddSignalRUserManagement(options =>
{
    options.BroadcastConnectionEvents = true;
});

// Ensure SignalR Communicator is registered
builder.Services.AddSignalRCommunicatorServer(options =>
{
    options.TimeoutSeconds = 30;
    options.MaxRetryAttempts = 3;
});

// Add SignalR Dashboard - IMPORTANT: Replace ApplicationUser and ApplicationDbContext with your types!
builder.Services.AddSignalRDashboard<ApplicationUser, ApplicationDbContext>();

// ... rest of configuration ...

var app = builder.Build();
```

**IMPORTANT:** Replace `ApplicationUser` with your IdentityUser type and `ApplicationDbContext` with your DbContext type!

### 4. Configure Render Mode (Blazor 8+)

For the dashboard to work properly with real-time updates, configure interactive server render mode in your `App.razor` or `Routes.razor`:

```razor
<!-- In App.razor or Routes.razor -->
<Routes @rendermode="InteractiveServer" />
```

Or configure per-page in your routing configuration if you prefer granular control.

### 5. Access the Dashboard

Navigate to `/signalr-dashboard` in your browser. The page is automatically registered and ready to use!

## Example: BlazorServerApplication Integration

For the `BlazorServerApplication` project in this solution:

```csharp
// In BlazorServerApplication/Program.cs

using BlazorServerApplication.Data; // Contains ApplicationUser
using Nesco.SignalRCommunicator.Dashboard;

// ... other usings ...

var builder = WebApplication.CreateBuilder(args);

// ... existing services ...

// Add Dashboard (already has UserManagement and Communicator configured)
builder.Services.AddSignalRDashboard<ApplicationUser, ApplicationDbContext>();

// ... rest of Program.cs ...
```

That's it! The dashboard will be available at `https://localhost:7000/signalr-dashboard`

## Features Available

Once integrated, you get:

### 1. Connection Monitoring
- View all connected users with usernames and emails
- See all active connections per user
- Connection timestamps and user agents
- Real-time connection statistics

### 2. Method Invocation Testing
- Invoke methods on all users
- Target specific users
- Target individual connections
- Test with JSON parameters

### 3. Quick Ping Tests
- Ping all connected users
- Ping specific user
- Ping individual connections

## Testing the Dashboard

### 1. Ensure Clients Are Connected

Make sure you have clients connected. For example, using the `BlazorWebassemblyApp` client:

1. Start the server: `https://localhost:7000`
2. Start the client: `https://localhost:5004`
3. Login to the client
4. The client should auto-connect via SignalR

### 2. Access the Dashboard

Navigate to: `https://localhost:7000/signalr-dashboard`

You should see:
- Connected users list with your logged-in user
- Connection details showing the WebAssembly client
- Forms to invoke methods

### 3. Test Method Invocation

Try invoking the `Ping` method:
1. Method Name: `Ping`
2. Target Type: `All Connected Users`
3. Parameters: (leave empty)
4. Click "Invoke Method"

You should see a success result with the ping response!

## Customization

### Custom Styling

The dashboard includes embedded CSS, but you can override styles:

```css
/* In your app's CSS */
.signalr-dashboard .card {
    border-radius: 12px; /* Override default */
}
```

### Custom User Display

The dashboard fetches username and email from Identity. To customize this, you can:

1. Create a derived service from `DashboardService<TUser>`
2. Override `GetConnectedUsersAsync()`
3. Register your custom service instead

## Troubleshooting

### Issue: Dashboard page not found (404)

**Solution:** Ensure the package is properly referenced and services are registered.

### Issue: No users showing

**Solutions:**
1. Check that clients are actually connected to the hub
2. Verify database connection contains active connections
3. Check that `UserConnectionDbContext` is properly configured

### Issue: Method invocations fail

**Solutions:**
1. Verify the method exists on the client
2. Check that client has registered the method handler
3. Ensure parameters are valid JSON
4. Check server logs for detailed error messages

### Issue: Build errors about missing types

**Solution:** Ensure all dependent packages are installed:

```bash
dotnet add package Nesco.SignalRUserManagement.Server
dotnet add package Nesco.SignalRCommunicator.Server
```

## Side-by-Side with Existing Code

This package is designed to work alongside your existing SignalRTest.razor page:

- **Old code** (`/signalr-test`): Custom implementation in BlazorServerApplication
- **New package** (`/signalr-dashboard`): Reusable component from NuGet package

Both can coexist without conflicts. The new package uses the same services but is self-contained.

## API Reference

### Service Registration

```csharp
services.AddSignalRDashboard<TUser, TDbContext>()
```

- **TUser**: Your IdentityUser type (e.g., `ApplicationUser`, `IdentityUser`)
- **TDbContext**: Your DbContext type that contains `Connections` and `ConnectedUsers` DbSets (e.g., `ApplicationDbContext`)
- **Returns**: IServiceCollection for chaining

### Dashboard Service Interface

```csharp
public interface IDashboardService
{
    Task<SignalRResponse> InvokeOnAllConnectedAsync(string methodName, object? parameter);
    Task<SignalRResponse> InvokeOnUserAsync(string userId, string methodName, object? parameter);
    Task<SignalRResponse> InvokeOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter);
    Task<SignalRResponse> InvokeOnConnectionAsync(string connectionId, string methodName, object? parameter);

    int GetConnectedUsersCount();
    int GetActiveConnectionsCount();
    bool IsUserConnected(string userId);

    Task<List<ConnectedUserInfo>> GetConnectedUsersAsync();
}
```

## Next Steps

1. Build and install the package in your project
2. Register the service in Program.cs
3. Navigate to `/signalr-dashboard`
4. Test with connected clients
5. Use for monitoring and debugging SignalR connections

## Support

For issues or questions, refer to the main project repository or documentation.
