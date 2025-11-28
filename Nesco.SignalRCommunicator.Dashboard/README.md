# Nesco.SignalRCommunicator.Dashboard

A ready-to-use Blazor dashboard component for monitoring and testing SignalR connections with both **User Management** and **Communicator** features.

## Features

- 📡 **Real-time Connection Monitoring** - View all connected users and their active connections
- 🚀 **Method Invocation Testing** - Test SignalR method calls on users or specific connections
- 📊 **Connection Statistics** - See user counts, connection counts, and connection history
- 🎯 **Multiple Targeting Options** - Invoke methods on all users, specific users, or individual connections
- 📡 **Ping Functionality** - Quick connectivity testing with built-in ping feature
- 🔄 **Auto-Refresh** - Keep connection data up-to-date

## Installation

```bash
dotnet add package Nesco.SignalRCommunicator.Dashboard
```

## Quick Start

### 1. Install Required Packages

Make sure you have the following packages installed:

```bash
dotnet add package Nesco.SignalRUserManagement.Server
dotnet add package Nesco.SignalRCommunicator.Server
dotnet add package Nesco.SignalRCommunicator.Dashboard
```

### 2. Configure Services in Program.cs

```csharp
using Nesco.SignalRCommunicator.Dashboard;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// ... other services ...

// Add SignalR User Management
builder.Services.AddSignalRUserManagement(options =>
{
    // Configure options
});

// Add SignalR Communicator
builder.Services.AddSignalRCommunicatorServer(options =>
{
    options.TimeoutSeconds = 30;
});

// Add SignalR Dashboard - replace ApplicationUser and ApplicationDbContext with your types
builder.Services.AddSignalRDashboard<ApplicationUser, ApplicationDbContext>();

// ... other services ...

var app = builder.Build();

// ... middleware configuration ...

app.Run();
```

### 3. Configure Render Mode

Ensure your application uses Interactive Server render mode. In `App.razor` or `Routes.razor`:

```razor
<Routes @rendermode="InteractiveServer" />
```

This is required for real-time updates and interactive features to work.

### 4. Access the Dashboard

Navigate to `/signalr-dashboard` in your Blazor Server application. The dashboard page is automatically registered and ready to use!

## Usage

### Monitoring Connections

The dashboard automatically displays all connected users with:
- Username and Email
- Number of active connections
- Connection IDs and user agents
- Last connection timestamps

### Invoking Methods

Use the **Invoke Method** form to test method calls:

1. **Method Name**: Enter the name of the method to invoke (e.g., `Ping`, `GetClientInfo`)
2. **Target Type**: Choose who should receive the invocation:
   - **All Connected Users**: Broadcast to everyone
   - **Single User**: Target a specific user (all their connections)
   - **Multiple Users**: Target multiple users (comma-separated IDs)
   - **Single Connection**: Target a specific connection ID
3. **Parameters**: Enter JSON parameters if required (e.g., `{"A": 5, "B": 3}`)

### Quick Ping

Use the **Ping** buttons to quickly test connectivity:
- **Ping All**: Tests all connected users
- **Ping User**: Tests a specific user
- **Ping Connection**: Tests a specific connection

## Requirements

- .NET 8.0 or later
- ASP.NET Core Identity
- Entity Framework Core
- Nesco.SignalRUserManagement.Server
- Nesco.SignalRCommunicator.Server

## Architecture

The dashboard works by:

1. **Querying the UserManagement database** for active connections
2. **Using the Communicator service** to invoke methods on clients
3. **Displaying results** in real-time

## Configuration Options

The dashboard service is registered as scoped and requires:

- `ISignalRCommunicatorService` - For invoking methods on clients
- `IUserConnectionService` - For querying connection state
- `UserConnectionDbContext` - For database access
- `UserManager<TUser>` - For fetching user details

## Example Client Methods

Your SignalR clients should implement methods like:

```csharp
public class ClientMethods
{
    public PingResponse Ping()
    {
        return new PingResponse
        {
            Message = "Pong",
            Timestamp = DateTime.UtcNow
        };
    }

    public ClientInfo GetClientInfo()
    {
        return new ClientInfo
        {
            Platform = Environment.OSVersion.Platform.ToString(),
            Timestamp = DateTime.UtcNow
        };
    }
}
```

## Troubleshooting

### Dashboard shows no users

- Ensure SignalR hubs are properly configured and running
- Check that clients are successfully connecting to the hub
- Verify database connection string is correct

### Method invocations fail

- Ensure the method exists on the client
- Check that the client has registered the method handler
- Verify parameters are in correct JSON format
- Check network connectivity between server and clients

### Dashboard is static / doesn't update

**Solution:** Ensure Interactive Server render mode is configured:
```razor
<!-- In App.razor or Routes.razor -->
<Routes @rendermode="InteractiveServer" />
```

## License

MIT License

## Support

For issues, questions, or contributions, please visit the GitHub repository.
