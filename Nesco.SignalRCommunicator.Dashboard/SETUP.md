# Quick Setup - SignalR Dashboard

## Issue: Page Not Found (404)

If you're seeing "page /signalr-dashboard is not found", follow these steps:

## Solution Steps

### 1. Build the Dashboard Package

From your IDE (Rider/Visual Studio) or command line with .NET 10 SDK:

```bash
dotnet build Nesco.SignalRCommunicator.Dashboard/Nesco.SignalRCommunicator.Dashboard.csproj
```

### 2. Rebuild BlazorServerApplication

After building the Dashboard package, rebuild the server application:

```bash
dotnet build BlazorServerApplication/BlazorServerApplication.csproj
```

Or use your IDE's build function.

### 3. Restart the Application

Stop and restart your BlazorServerApplication. The page should now be available at:

```
https://localhost:7000/signalr-dashboard
```

## What Was Configured

The integration is already complete in `BlazorServerApplication`:

### ✅ Program.cs (Line 10, 123, 170)
```csharp
using Nesco.SignalRCommunicator.Dashboard;

// Service registration
builder.Services.AddSignalRDashboard<ApplicationUser, ApplicationDbContext>();

// Component discovery
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Nesco.SignalRCommunicator.Dashboard.ServiceCollectionExtensions).Assembly);
```

### ✅ BlazorServerApplication.csproj (Line 28)
```xml
<ProjectReference Include="..\Nesco.SignalRCommunicator.Dashboard\Nesco.SignalRCommunicator.Dashboard.csproj" />
```

### ✅ App.razor
Already configured with InteractiveServer render mode (no changes needed).

## Verification

Once running, you should see:

1. **Old page still works**: `https://localhost:7000/signalr-test`
2. **New package page works**: `https://localhost:7000/signalr-dashboard`

Both pages show identical functionality but use different implementations:
- `/signalr-test` - Local implementation in BlazorServerApplication
- `/signalr-dashboard` - Reusable package from Nesco.SignalRCommunicator.Dashboard

## Troubleshooting

### Still getting 404?

1. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build
   ```

2. **Check the output**:
   Look for: "Building Nesco.SignalRCommunicator.Dashboard..." in build output

3. **Verify files exist**:
   Check that `Nesco.SignalRCommunicator.Dashboard/bin/Debug/net10.0/` contains the built DLL

4. **Check static assets**:
   After build, verify CSS file exists in the output directory

### CSS not loading?

The CSS is referenced as:
```html
<link href="_content/Nesco.SignalRCommunicator.Dashboard/css/dashboard.css" rel="stylesheet" />
```

This should be automatically served by the Blazor static asset middleware.

## Testing

1. **Start BlazorServerApplication**: `dotnet run --project BlazorServerApplication`
2. **Start BlazorWebassemblyApp**: `dotnet run --project BlazorWebassemblyApp`
3. **Login to the client**: This creates a SignalR connection
4. **Open both dashboards**:
   - Old: `https://localhost:7000/signalr-test`
   - New: `https://localhost:7000/signalr-dashboard`
5. **Verify**: Both should show the connected user

## Build Commands (For Reference)

Since you're using .NET 10:

```bash
# Build everything from solution root
dotnet build IntranetBlazor.sln

# Build just the Dashboard
dotnet build Nesco.SignalRCommunicator.Dashboard

# Build BlazorServerApplication
dotnet build BlazorServerApplication

# Create NuGet package (optional)
dotnet pack Nesco.SignalRCommunicator.Dashboard -c Release
```

## Success Indicators

When working correctly:

✅ No build errors
✅ `/signalr-dashboard` route is found
✅ Dashboard displays with proper styling
✅ Connected users appear in the table
✅ Ping buttons work
✅ Method invocation works
✅ Both old and new pages work simultaneously
