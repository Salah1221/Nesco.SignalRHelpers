# Nesco.SignalRCommunicator.Server

Server library for SignalR-based communication. Invoke methods on connected clients and receive responses with automatic large data handling.

## Installation

```bash
dotnet add package Nesco.SignalRCommunicator.Server
dotnet add package Nesco.SignalRCommunicator.Core
```

## Quick Start

### 1. Register Services in Program.cs

```csharp
using Nesco.SignalRCommunicator.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR Communicator Server
builder.Services.AddSignalRCommunicatorServer(options =>
{
    options.MaxConcurrentRequests = 10;
    options.RequestTimeoutSeconds = 300; // 5 minutes
    options.SemaphoreTimeoutSeconds = 5;
    options.AutoDeleteTempFiles = true;
    options.TempFolder = "signalr-temp";
});

var app = builder.Build();

// Map the SignalR hub
app.MapSignalRCommunicatorHub("/hubs/communicator");

app.Run();
```

### 2. Inject and Use the Service

```csharp
using Nesco.SignalRCommunicator.Server.Services;

public class MyController : ControllerBase
{
    private readonly ISignalRCommunicatorService _communicator;
    private readonly ILogger<MyController> _logger;

    public MyController(
        ISignalRCommunicatorService communicator,
        ILogger<MyController> logger)
    {
        _communicator = communicator;
        _logger = logger;
    }

    [HttpPost("process-data")]
    public async Task<IActionResult> ProcessData([FromBody] DataRequest request)
    {
        // Check if any clients are connected
        if (!_communicator.HasConnectedClients())
        {
            return BadRequest("No clients connected");
        }

        try
        {
            // Invoke method on client and get typed response
            var result = await _communicator.InvokeMethodAsync<DataResponse>(
                "ProcessRequest",
                request
            );

            if (result == null)
            {
                return BadRequest("Client returned no data or error occurred");
            }

            return Ok(result);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Request timed out");
            return StatusCode(504, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking client method");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        if (!_communicator.HasConnectedClients())
        {
            return BadRequest("No clients connected");
        }

        var response = await _communicator.InvokeMethodAsync<TestResponse>(
            "TestConnection",
            null
        );

        return Ok(response);
    }
}
```

### 3. Configuration from appsettings.json (Optional)

```json
{
  "SignalRServer": {
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 300,
    "SemaphoreTimeoutSeconds": 5,
    "TempFolder": "signalr-temp",
    "AutoDeleteTempFiles": true
  }
}
```

```csharp
builder.Services.AddSignalRCommunicatorServer(options =>
{
    builder.Configuration.GetSection("SignalRServer").Bind(options);
});
```

## Advanced Usage

### Custom File Reader Service

For custom storage solutions (Azure Blob, AWS S3, etc.):

```csharp
public class AzureBlobFileReaderService : IFileReaderService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobFileReaderService> _logger;

    public AzureBlobFileReaderService(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobFileReaderService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string> ReadFileAsync(string filePath)
    {
        var blobClient = new BlobClient(new Uri(filePath));

        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Blob not found: {filePath}");
        }

        var downloadResult = await blobClient.DownloadContentAsync();
        return downloadResult.Value.Content.ToString();
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(filePath));
            return await blobClient.ExistsAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(filePath));
            await blobClient.DeleteIfExistsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob: {FilePath}", filePath);
            return false;
        }
    }
}

// Register with custom file reader
builder.Services.AddSignalRCommunicatorServer<AzureBlobFileReaderService>(options =>
{
    options.MaxConcurrentRequests = 20;
    options.RequestTimeoutSeconds = 600;
});
```

### Custom Web Root Path

If your files are stored in a custom location:

```csharp
builder.Services.AddSignalRCommunicatorServer(
    webRootPath: "/custom/path/to/files",
    configureOptions: options =>
    {
        options.AutoDeleteTempFiles = true;
    }
);
```

### Working with Raw Responses

If you need access to the raw `SignalRResponse` object:

```csharp
public async Task<IActionResult> ProcessWithRawResponse()
{
    var response = await _communicator.InvokeMethodAsync("GetData", new { Id = 123 });

    switch (response.ResponseType)
    {
        case SignalRResponseType.JsonObject:
            // Handle JSON data
            var data = JsonSerializer.Deserialize<MyData>(
                JsonSerializer.Serialize(response.JsonData)
            );
            return Ok(data);

        case SignalRResponseType.FilePath:
            // Handle file path
            return Ok(new { FilePath = response.FilePath });

        case SignalRResponseType.Error:
            // Handle error
            return BadRequest(response.ErrorMessage);

        case SignalRResponseType.Null:
            // Handle null response
            return NoContent();

        default:
            return StatusCode(500, "Unknown response type");
    }
}
```

### Multiple Client Broadcasting

The service automatically broadcasts to all connected clients and returns the first response. For scenarios where you need responses from all clients, you can extend the hub:

```csharp
public class CustomCommunicatorHub : SignalRCommunicatorHub
{
    public async Task<List<SignalRResponse>> BroadcastAndCollectAll(string requestId, string methodName, object? parameter)
    {
        var responses = new List<SignalRResponse>();
        // Custom implementation for collecting from all clients
        return responses;
    }
}
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxConcurrentRequests` | int | 10 | Maximum simultaneous requests to clients |
| `RequestTimeoutSeconds` | int | 300 (5 min) | Maximum time to wait for client response |
| `SemaphoreTimeoutSeconds` | int | 5 | Maximum time to wait for a semaphore slot |
| `TempFolder` | string | "signalr-temp" | Folder name for temporary files |
| `AutoDeleteTempFiles` | bool | true | Automatically delete temp files after reading |

## Connection Management

### Monitoring Connected Clients

```csharp
public class HealthCheckService
{
    private readonly ISignalRCommunicatorService _communicator;

    public HealthCheckService(ISignalRCommunicatorService communicator)
    {
        _communicator = communicator;
    }

    public async Task<HealthStatus> CheckHealth()
    {
        if (!_communicator.HasConnectedClients())
        {
            return HealthStatus.Unhealthy;
        }

        try
        {
            // Try a test connection
            var response = await _communicator.InvokeMethodAsync<object>(
                "TestConnection",
                null
            );

            return response != null ? HealthStatus.Healthy : HealthStatus.Degraded;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }
}
```

### Hub Access Count

```csharp
using Nesco.SignalRCommunicator.Server.Hubs;

public class ConnectionMonitor
{
    public int GetConnectedClientsCount()
    {
        return SignalRCommunicatorHub.ConnectedClientsCount;
    }
}
```

## Features

✅ **Concurrent Request Management**: Semaphore-based throttling prevents resource exhaustion
✅ **Timeout Handling**: Configurable timeouts with automatic cleanup
✅ **Smart Data Handling**: Automatically handles both direct and file-based responses
✅ **Auto Cleanup**: Optional automatic deletion of temporary files
✅ **Type-Safe Responses**: Generic methods for type-safe response handling
✅ **Connection Monitoring**: Built-in client connection tracking
✅ **Error Handling**: Comprehensive error handling and logging
✅ **Thread-Safe**: Safe for concurrent requests

## Best Practices

1. **Always Check for Clients**: Use `HasConnectedClients()` before invoking methods
2. **Set Appropriate Timeouts**: Match timeouts to expected method execution times
3. **Handle All Response Types**: Always handle errors and null responses
4. **Use Generic Methods**: Prefer `InvokeMethodAsync<T>` for type safety
5. **Monitor Performance**: Watch semaphore utilization and timeout rates
6. **Clean Up Temp Files**: Enable `AutoDeleteTempFiles` or implement cleanup jobs
7. **Log Extensively**: Use logging to diagnose issues

## Error Handling

### Common Exceptions

- **`TimeoutException`**: Client didn't respond within the configured timeout
- **`InvalidOperationException`**: Maximum concurrent requests reached
- **`FileNotFoundException`**: File path response but file doesn't exist

### Example Error Handling

```csharp
public async Task<IActionResult> RobustInvocation()
{
    try
    {
        if (!_communicator.HasConnectedClients())
        {
            return Problem(
                statusCode: 503,
                title: "Service Unavailable",
                detail: "No clients are currently connected"
            );
        }

        var result = await _communicator.InvokeMethodAsync<DataResponse>(
            "ProcessData",
            new DataRequest { Id = 123 }
        );

        if (result == null)
        {
            return Problem(
                statusCode: 500,
                title: "Processing Failed",
                detail: "Client returned an error or null response"
            );
        }

        return Ok(result);
    }
    catch (TimeoutException)
    {
        return Problem(
            statusCode: 504,
            title: "Gateway Timeout",
            detail: "Client did not respond within the timeout period"
        );
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("maximum concurrent requests"))
    {
        return Problem(
            statusCode: 503,
            title: "Service Busy",
            detail: "Server is currently processing maximum requests, please try again"
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error invoking client method");
        return Problem(
            statusCode: 500,
            title: "Internal Error",
            detail: "An unexpected error occurred"
        );
    }
}
```

## Performance Considerations

### Concurrency Limits

The `MaxConcurrentRequests` setting controls how many simultaneous requests can be in-flight. Tune this based on:
- Client processing capacity
- Network bandwidth
- Server resources

### Timeout Configuration

Set `RequestTimeoutSeconds` based on:
- Expected method execution time
- Network latency
- Client resource availability

### File Cleanup

Enable `AutoDeleteTempFiles` to prevent disk space issues, or implement a cleanup job:

```csharp
public class TempFileCleanupService : BackgroundService
{
    private readonly IFileReaderService _fileReader;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Clean up files older than 1 hour
            await CleanupOldTempFiles();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

## Troubleshooting

### No Clients Connected

- Verify client is running and configured correctly
- Check hub path matches on both client and server
- Review network connectivity and firewall rules
- Check server logs for connection attempts

### Timeout Errors

- Increase `RequestTimeoutSeconds` if methods take longer
- Check client logs to see if method is executing
- Verify client has sufficient resources
- Consider optimizing method execution time

### Memory/Disk Issues

- Enable `AutoDeleteTempFiles`
- Implement periodic cleanup of temp folder
- Monitor disk space
- Consider cloud storage for large files

## License

MIT License - see LICENSE file for details.
