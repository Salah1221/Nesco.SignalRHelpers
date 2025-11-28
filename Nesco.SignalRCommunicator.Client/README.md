# Nesco.SignalRCommunicator.Client

Client library for SignalR-based communication. Connect to a SignalR server, receive method invocations, and return responses with automatic large data handling.

## Installation

```bash
dotnet add package Nesco.SignalRCommunicator.Client
dotnet add package Nesco.SignalRCommunicator.Core
```

## Quick Start

### 1. Implement IMethodExecutor

Create a class that routes method calls to your business logic:

```csharp
public class MyMethodExecutor : IMethodExecutor
{
    private readonly ILogger<MyMethodExecutor> _logger;
    private readonly IMyBusinessService _businessService;

    public MyMethodExecutor(
        ILogger<MyMethodExecutor> logger,
        IMyBusinessService businessService)
    {
        _logger = logger;
        _businessService = businessService;
    }

    public async Task<object?> ExecuteAsync(string methodName, object? parameter)
    {
        _logger.LogInformation("Executing method: {MethodName}", methodName);

        return methodName switch
        {
            "GetData" => await _businessService.GetData(ConvertParameter<int>(parameter)),
            "ProcessRequest" => await _businessService.Process(ConvertParameter<RequestDto>(parameter)),
            "TestConnection" => new { Status = "OK", Message = "Connected successfully" },
            _ => throw new NotSupportedException($"Method '{methodName}' is not supported")
        };
    }

    private T ConvertParameter<T>(object? parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        if (parameter is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText())!;
        }

        if (parameter is T directCast)
        {
            return directCast;
        }

        var json = JsonSerializer.Serialize(parameter);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
```

### 2. Register Services in Program.cs

```csharp
using Nesco.SignalRCommunicator.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register your business services
builder.Services.AddSingleton<IMyBusinessService, MyBusinessService>();

// Register the SignalR communicator client with your method executor
builder.Services.AddSignalRCommunicatorClientWithExecutor<MyMethodExecutor>(options =>
{
    options.ServerUrl = "https://myserver.com";
    options.HubPath = "/hubs/communicator";
    options.MaxDirectDataSizeBytes = 20 * 1024; // 20KB
    options.KeepAliveIntervalSeconds = 15;
    options.ServerTimeoutSeconds = 30;
    options.ReconnectDelaySeconds = 30;
    options.MaxRetryAttempts = 3;
    options.RetryDelaySeconds = 5;
});

var app = builder.Build();
app.Run();
```

### 3. Configuration from appsettings.json (Optional)

You can also configure options from your configuration file:

```json
{
  "SignalRClient": {
    "ServerUrl": "https://myserver.com",
    "HubPath": "/hubs/communicator",
    "MaxDirectDataSizeBytes": 20480,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    "ReconnectDelaySeconds": 30,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "TempFolder": "signalr-temp"
  }
}
```

```csharp
builder.Services.AddSignalRCommunicatorClientWithExecutor<MyMethodExecutor>(options =>
{
    builder.Configuration.GetSection("SignalRClient").Bind(options);
});
```

## Advanced Usage

### Custom File Upload Service

If you need to use a custom storage solution (e.g., Azure Blob Storage, AWS S3):

```csharp
public class AzureBlobFileUploadService : IFileUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobFileUploadService> _logger;

    public AzureBlobFileUploadService(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobFileUploadService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(byte[] fileData, string fileName, string folder = "signalr-temp")
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(folder);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(fileName);
        using var stream = new MemoryStream(fileData);
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobClient.Uri.ToString();
    }

    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string folder = "signalr-temp")
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(folder);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobClient.Uri.ToString();
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

// Register with custom method executor and file upload service
builder.Services.AddSignalRCommunicatorClientWithExecutor<MyMethodExecutor, AzureBlobFileUploadService>(options =>
{
    options.ServerUrl = "https://myserver.com";
    options.HubPath = "/hubs/communicator";
});
```

### Manual Connection Control

If you need to manually control the connection (not recommended for most scenarios):

```csharp
public class MyService
{
    private readonly ISignalRCommunicatorClient _client;

    public MyService(ISignalRCommunicatorClient client)
    {
        _client = client;
    }

    public async Task ConnectAsync()
    {
        if (!_client.IsConnected)
        {
            await _client.StartAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        await _client.StopAsync();
    }
}
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ServerUrl` | string | "https://localhost:5004" | The URL of the SignalR server |
| `HubPath` | string | "/hubs/communicator" | The path to the SignalR hub |
| `KeepAliveIntervalSeconds` | int | 15 | How often to send ping messages |
| `ServerTimeoutSeconds` | int | 30 | How long to wait before considering the connection dead |
| `ReconnectDelaySeconds` | int | 30 | Delay before attempting to reconnect |
| `MaxDirectDataSizeBytes` | int | 10240 (10KB) | Maximum size for direct transmission |
| `MaxRetryAttempts` | int | 3 | Maximum retry attempts for initial connection |
| `RetryDelaySeconds` | int | 5 | Delay between retry attempts |
| `TempFolder` | string | "signalr-temp" | Folder name for temporary file uploads |

## Features

✅ **Automatic Connection Management**: Background service handles connection lifecycle
✅ **Auto-Reconnection**: Automatically reconnects on connection loss
✅ **Smart Data Handling**: Automatically chooses between direct transmission and file upload based on size
✅ **Retry Logic**: Configurable retry attempts for robust connectivity
✅ **Method Routing**: Flexible method executor pattern for clean code organization
✅ **Error Handling**: Comprehensive error handling and logging
✅ **Thread-Safe**: Safe for concurrent use

## Best Practices

1. **Keep Methods Simple**: Each method should do one thing well
2. **Use DTOs**: Always use Data Transfer Objects for parameters and returns
3. **Handle Errors Gracefully**: Always wrap your business logic in try-catch blocks
4. **Log Extensively**: Use the built-in logging for debugging
5. **Set Appropriate Timeouts**: Adjust timeouts based on your method execution times
6. **Monitor Connection State**: Use `IsConnected` property for health checks

## Troubleshooting

### Connection Fails on Startup

- Verify the server URL and hub path are correct
- Check if the server is running and accessible
- Review firewall and network settings
- Check server logs for connection attempts

### Methods Not Executing

- Verify method name matches exactly (case-sensitive)
- Ensure parameter types can be deserialized
- Check server logs to see if method is being invoked
- Verify IMethodExecutor is registered correctly

### Large Data Not Uploading

- Verify file upload endpoint exists on the server
- Check HttpClient configuration
- Ensure server has sufficient disk space
- Review file upload service logs

## License

MIT License - see LICENSE file for details.
