# Nesco.SignalRCommunicator.Core

Core library for SignalR-based client-server communication with support for large data transfer via file uploads.

## Overview

This package contains the shared models, interfaces, and configuration options used by both the client and server packages.

## Installation

```bash
dotnet add package Nesco.SignalRCommunicator.Core
```

## What's Included

### Models

- **`SignalRResponse`**: Standardized response object for method invocations
  - Supports multiple response types: JsonObject, FilePath, Null, Error
  - Handles both direct data transmission and file-based transfers for large data

- **`SignalRResponseType`**: Enum defining response types

### Interfaces

- **`IMethodExecutor`**: Contract for executing methods on the client side
  - Implement this interface to route method calls to your business logic

- **`IFileUploadService`**: Contract for uploading large response data
  - Implement for custom storage solutions (cloud, network shares, etc.)

- **`IFileReaderService`**: Contract for reading uploaded files on the server
  - Implement for custom file retrieval logic

### Options

- **`SignalRClientOptions`**: Configuration for client behavior
  - Server URL and hub path
  - Timeouts and retry logic
  - Maximum direct data size threshold

- **`SignalRServerOptions`**: Configuration for server behavior
  - Maximum concurrent requests
  - Request timeout
  - Automatic file cleanup

## Response Types Explained

### JsonObject
Used when the response data is small enough to be sent directly through SignalR (default: ≤10KB).

```csharp
var response = new SignalRResponse
{
    ResponseType = SignalRResponseType.JsonObject,
    JsonData = myDataObject
};
```

### FilePath
Used when the response data exceeds the size threshold. Data is uploaded to a file, and the path is returned.

```csharp
var response = new SignalRResponse
{
    ResponseType = SignalRResponseType.FilePath,
    FilePath = "/uploads/temp/result_abc123.json"
};
```

### Null
Used when a method returns null or no data.

```csharp
var response = new SignalRResponse
{
    ResponseType = SignalRResponseType.Null
};
```

### Error
Used when an error occurs during method execution.

```csharp
var response = new SignalRResponse
{
    ResponseType = SignalRResponseType.Error,
    ErrorMessage = "Failed to process request: Database connection timeout"
};
```

## Implementing IMethodExecutor

The method executor is responsible for routing method calls to your business logic:

```csharp
public class MyMethodExecutor : IMethodExecutor
{
    private readonly IMyService _myService;

    public MyMethodExecutor(IMyService myService)
    {
        _myService = myService;
    }

    public async Task<object?> ExecuteAsync(string methodName, object? parameter)
    {
        return methodName switch
        {
            "GetUserData" => await _myService.GetUserData(ConvertParameter<int>(parameter)),
            "UpdateSettings" => await _myService.UpdateSettings(ConvertParameter<SettingsDto>(parameter)),
            "ProcessOrder" => await _myService.ProcessOrder(ConvertParameter<OrderDto>(parameter)),
            _ => throw new NotSupportedException($"Method '{methodName}' is not supported")
        };
    }

    private T ConvertParameter<T>(object? parameter)
    {
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

## Thread Safety

All models and options are designed to be thread-safe when used as intended. The response handling mechanism uses concurrent collections to manage multiple simultaneous requests.

## License

MIT License - see LICENSE file for details.
