# Nesco.SignalRUserManagement.Client

Client-side package for connecting to SignalR User Management hubs from Blazor WebAssembly applications.

## Installation

```bash
dotnet add package Nesco.SignalRUserManagement.Client
```

## Usage

### 1. Configure services in Program.cs

```csharp
using Nesco.SignalRUserManagement.Client.Extensions;

builder.Services.AddUserManagementClient(options =>
{
    options.BroadcastConnectionEvents = true;
    options.ConnectionEventMethod = "UserConnectionEvent";
});
```

### 2. Initialize the connection

```csharp
@inject UserConnectionClient ConnectionClient

protected override async Task OnInitializedAsync()
{
    // Simple initialization
    await ConnectionClient.InitializeAsync("https://yourserver.com/hubs/messaging");

    // With authentication
    await ConnectionClient.InitializeAsync(
        "https://yourserver.com/hubs/messaging",
        async () => await GetAccessTokenAsync());

    // Subscribe to connection status changes
    ConnectionClient.ConnectionStatusChanged += OnConnectionStatusChanged;

    // Subscribe to user connection events
    ConnectionClient.UserConnectionEventReceived += OnUserConnectionEvent;

    // Register message handlers
    ConnectionClient.On<string>("ReceiveNotification", async (message) =>
    {
        Console.WriteLine($"Received: {message}");
        StateHasChanged();
    });
}

private void OnConnectionStatusChanged(bool isConnected)
{
    Console.WriteLine($"Connection status: {(isConnected ? "Connected" : "Disconnected")}");
    StateHasChanged();
}

private Task OnUserConnectionEvent(UserConnectionEventArgs eventArgs)
{
    Console.WriteLine($"User {eventArgs.UserId} {eventArgs.EventType}");
    return Task.CompletedTask;
}
```

### 3. Send messages to the server

```csharp
// Send a message to the server
await ConnectionClient.SendAsync("SendMessage", "Hello server!");

// Invoke a method and get a result
var result = await ConnectionClient.InvokeAsync<string>("GetServerTime");
```

### 4. Check connection status

```csharp
bool isConnected = ConnectionClient.IsConnected;
string? connectionId = ConnectionClient.ConnectionId;
var state = ConnectionClient.ConnectionState;
```

### 5. Manual reconnection

```csharp
if (!ConnectionClient.IsConnected)
{
    await ConnectionClient.ReconnectAsync();
}
```

## Complete Example

```csharp
@page "/notifications"
@inject UserConnectionClient ConnectionClient
@implements IAsyncDisposable

<h3>Notifications</h3>

<p>Status: @(ConnectionClient.IsConnected ? "Connected" : "Disconnected")</p>
<p>Connection ID: @ConnectionClient.ConnectionId</p>

<ul>
    @foreach (var notification in _notifications)
    {
        <li>@notification</li>
    }
</ul>

@code {
    private List<string> _notifications = new();

    protected override async Task OnInitializedAsync()
    {
        // Initialize connection
        await ConnectionClient.InitializeAsync(
            "https://localhost:5004/hubs/messaging",
            () => Task.FromResult(AccessToken));

        // Subscribe to events
        ConnectionClient.ConnectionStatusChanged += (isConnected) =>
        {
            StateHasChanged();
        };

        // Register message handlers
        ConnectionClient.On<string>("ReceiveNotification", async (message) =>
        {
            _notifications.Add(message);
            StateHasChanged();
        });

        ConnectionClient.On<UserConnectionEventArgs>("UserConnectionEvent", async (eventArgs) =>
        {
            _notifications.Add($"User {eventArgs.UserId} {eventArgs.EventType}");
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        await ConnectionClient.StopAsync();
    }
}
```

## Features

- **Automatic Reconnection**: Automatically reconnects with exponential backoff
- **Connection Events**: Receive notifications when users connect/disconnect
- **Type-safe Message Handling**: Register strongly-typed message handlers
- **Authentication Support**: Integrate with JWT or other authentication mechanisms
- **Connection Status Monitoring**: Track connection state changes in real-time
- **Manual Control**: Start, stop, and reconnect as needed

## Events

### ConnectionStatusChanged
Raised when the connection state changes (connected/disconnected).

```csharp
ConnectionClient.ConnectionStatusChanged += (isConnected) =>
{
    Console.WriteLine($"Connection: {isConnected}");
};
```

### UserConnectionEventReceived
Raised when another user connects or disconnects (if BroadcastConnectionEvents is enabled).

```csharp
ConnectionClient.UserConnectionEventReceived += async (eventArgs) =>
{
    Console.WriteLine($"{eventArgs.UserId} {eventArgs.EventType}");
};
```

## Connection States

- **Disconnected**: No connection to the server
- **Connecting**: Connection is being established
- **Connected**: Successfully connected and can send/receive messages
- **Reconnecting**: Connection was lost and is being re-established

## Notes

- The client automatically reconnects with exponential backoff (0s, 2s, 5s, 10s)
- All message handlers are executed asynchronously
- The service is registered as a singleton and can be injected anywhere
- Connection status changes trigger events that can update the UI
- Dispose the client properly to clean up resources
