# Nesco.SignalRUserManagement.Server

Server-side package for managing SignalR user connections with database persistence.

## Installation

```bash
dotnet add package Nesco.SignalRUserManagement.Server
```

## Database Setup

### 1. Add the models to your DbContext

```csharp
using Nesco.SignalRUserManagement.Server.Models;

public class ApplicationDbContext : DbContext
{
    public DbSet<ConnectedUser> ConnectedUsers { get; set; }
    public DbSet<Connection> Connections { get; set; }

    // ... your other DbSets
}
```

### 2. Create and apply migration

```bash
dotnet ef migrations add AddUserManagement
dotnet ef database update
```

## Usage

### 1. Create your hub by inheriting from UserManagementHub

```csharp
using Nesco.SignalRUserManagement.Server.Hubs;

public class MessagingHub : UserManagementHub<ApplicationDbContext>
{
    public MessagingHub(
        ApplicationDbContext context,
        ILogger<MessagingHub> logger,
        IOptions<UserManagementOptions> options)
        : base(context, logger, options)
    {
    }

    // Add your custom hub methods here
}
```

### 2. Configure services in Program.cs

```csharp
using Nesco.SignalRUserManagement.Server.Extensions;

// Add user management services
builder.Services.AddUserManagement<MessagingHub, ApplicationDbContext>(options =>
{
    options.BroadcastConnectionEvents = true;
    options.ConnectionEventMethod = "UserConnectionEvent";
    options.AutoPurgeOfflineConnections = true;
    options.KeepAliveIntervalSeconds = 15;
    options.ClientTimeoutSeconds = 30;
    options.TrackUserAgent = true;
});

// Map the hub
app.MapUserManagementHub<MessagingHub>("/hubs/messaging");
```

### 3. Use IUserConnectionService to send messages

```csharp
public class NotificationService
{
    private readonly IUserConnectionService _userConnection;

    public NotificationService(IUserConnectionService userConnection)
    {
        _userConnection = userConnection;
    }

    // Send to all connected clients
    public async Task NotifyAll(string message)
    {
        await _userConnection.SendToAllAsync("ReceiveNotification", message);
    }

    // Send to specific user (all their connections)
    public async Task NotifyUser(string userId, string message)
    {
        await _userConnection.SendToUserAsync(userId, "ReceiveNotification", message);
    }

    // Send to specific connection
    public async Task NotifyConnection(string connectionId, string message)
    {
        await _userConnection.SendToConnectionAsync(connectionId, "ReceiveNotification", message);
    }

    // Send to multiple users
    public async Task NotifyUsers(List<string> userIds, string message)
    {
        await _userConnection.SendToUsersAsync(userIds, "ReceiveNotification", message);
    }

    // Check connection status
    public bool IsUserOnline(string userId)
    {
        return _userConnection.IsUserConnected(userId);
    }
}
```

### 4. Query connected users

```csharp
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    [HttpGet("GetConnectedUsers")]
    public async Task<ActionResult<IEnumerable<ConnectedUserDTO>>> GetConnectedUsers()
    {
        var users = await _context.ConnectedUsers
            .Include(u => u.Connections)
            .ToListAsync();

        var usersDTO = users.Select(u => new ConnectedUserDTO
        {
            UserId = u.UserId,
            LastConnect = u.LastConnect,
            LastDisconnect = u.LastDisconnect,
            Connections = u.Connections.Select(c => new ConnectionDTO
            {
                ConnectionId = c.ConnectionId,
                UserAgent = c.UserAgent,
                Connected = c.Connected,
                ConnectedAt = c.ConnectedAt
            }).ToList()
        }).ToList();

        return Ok(usersDTO);
    }
}
```

## Configuration Options

- **BroadcastConnectionEvents**: Whether to broadcast connection/disconnection events to all clients (default: true)
- **ConnectionEventMethod**: Method name for connection event broadcasts (default: "UserConnectionEvent")
- **AutoPurgeOfflineConnections**: Whether to automatically clean up offline connections (default: true)
- **KeepAliveIntervalSeconds**: Keep-alive interval for SignalR connections (default: 15)
- **ClientTimeoutSeconds**: Client timeout in seconds (default: 30)
- **TrackUserAgent**: Whether to store user agent information (default: true)

## Event Handling

The hub automatically broadcasts connection events if `BroadcastConnectionEvents` is enabled. Clients will receive events with this structure:

```csharp
{
    "userId": "user123",
    "connectionId": "connection456",
    "userAgent": "Mozilla/5.0...",
    "eventType": "Connected", // or "Disconnected"
    "timestamp": "2025-01-01T12:00:00Z"
}
```

## Notes

- The hub uses `Context.UserIdentifier` to track users, which is typically the authenticated user ID
- Connections are automatically cleaned up on disconnect
- The service is scoped to work with your DbContext lifecycle
- All database operations are async and transactional
