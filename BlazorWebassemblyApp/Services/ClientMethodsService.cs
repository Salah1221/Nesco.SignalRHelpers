using System.Text.Json;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Client.Dashboard.Services;

namespace BlazorWebassemblyApp.Services;

/// <summary>
/// Service containing methods that can be invoked by the server via SignalR
/// Implements IMethodExecutor to route method calls from the server to appropriate handlers
/// </summary>
public class ClientMethodsService : IMethodExecutor
{
    private readonly ILogger<ClientMethodsService> _logger;
    private readonly MethodInvocationLogger _legacyLogger; // Keep for backwards compatibility with Home.razor
    private readonly IMethodInvocationLogger _dashboardLogger; // Package logger for ClientDashboard

    public ClientMethodsService(
        ILogger<ClientMethodsService> logger,
        MethodInvocationLogger legacyLogger,
        IMethodInvocationLogger dashboardLogger)
    {
        _logger = logger;
        _legacyLogger = legacyLogger;
        _dashboardLogger = dashboardLogger;
    }

    /// <summary>
    /// Executes a method based on the method name received from the server
    /// </summary>
    public async Task<object?> ExecuteAsync(string methodName, object? parameter)
    {
        _logger.LogInformation(">>> [CLIENT METHOD] Executing: {MethodName}", methodName);

        var startTime = DateTime.Now;
        object? result = null;
        Exception? error = null;

        try
        {
            result = methodName switch
            {
                "Ping" => Ping(), // Default method for connectivity check
                "GetClientInfo" => GetClientInfo(),
                "Calculate" => Calculate(ParseParameter<CalculationRequest>(parameter)),
                "GetStatus" => GetStatus(),
                "ProcessData" => await ProcessDataAsync(ParseParameter<ProcessRequest>(parameter)),
                "SimulateDelay" => await SimulateDelayAsync(ParseParameter<DelayRequest>(parameter)),
                _ => throw new NotSupportedException($"Method '{methodName}' is not supported")
            };

            var duration = DateTime.Now - startTime;
            _logger.LogInformation(">>> [CLIENT METHOD] Completed: {MethodName} in {Duration}ms", methodName, duration.TotalMilliseconds);

            // Log to both invocation loggers (legacy for Home.razor, package for ClientDashboard)
            var paramJson = parameter != null ? JsonSerializer.Serialize(parameter) : null;
            var resultJson = result != null ? JsonSerializer.Serialize(result) : null;

            _legacyLogger.LogInvocation(methodName, parameter, result, duration);
            _dashboardLogger.Log(methodName, paramJson, resultJson, null, true, duration);

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            error = ex;
            _logger.LogError(ex, ">>> [CLIENT METHOD] Error executing: {MethodName}", methodName);

            // Log error to both invocation loggers
            var paramJson = parameter != null ? JsonSerializer.Serialize(parameter) : null;

            _legacyLogger.LogInvocation(methodName, parameter, null, duration, error);
            _dashboardLogger.Log(methodName, paramJson, null, ex.Message, false, duration);

            throw;
        }
    }

    /// <summary>
    /// Helper method to parse and deserialize parameters
    /// </summary>
    private T ParseParameter<T>(object? parameter) where T : new()
    {
        if (parameter == null)
        {
            return new T();
        }

        try
        {
            // If parameter is already a JsonElement, deserialize it
            if (parameter is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? new T();
            }

            // If it's already the correct type, return it
            if (parameter is T typedParam)
            {
                return typedParam;
            }

            // Try to serialize and deserialize to convert
            var json = JsonSerializer.Serialize(parameter);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse parameter for type {Type}", typeof(T).Name);
            throw new ArgumentException($"Failed to parse parameter to type {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Default connectivity check method - returns "Pong" to confirm client is online
    /// </summary>
    public PingResponse Ping()
    {
        _logger.LogInformation("Ping invoked");

        return new PingResponse
        {
            Message = "Pong",
            Timestamp = DateTime.UtcNow,
            ClientType = "Blazor WebAssembly"
        };
    }

    /// <summary>
    /// Simple method that returns client information
    /// </summary>
    public ClientInfo GetClientInfo()
    {
        _logger.LogInformation("GetClientInfo invoked");

        return new ClientInfo
        {
            ClientType = "Blazor WebAssembly",
            Platform = OperatingSystem.IsBrowser() ? "Browser" : "Unknown",
            Timestamp = DateTime.Now,
            UserAgent = "Blazor WASM Client"
        };
    }

    /// <summary>
    /// Method with parameters that performs calculation
    /// </summary>
    public int Calculate(CalculationRequest request)
    {
        _logger.LogInformation("Calculate invoked with A={A}, B={B}, Operation={Operation}",
            request.A, request.B, request.Operation);

        return request.Operation?.ToLower() switch
        {
            "add" => request.A + request.B,
            "subtract" => request.A - request.B,
            "multiply" => request.A * request.B,
            "divide" => request.B != 0 ? request.A / request.B : 0,
            _ => throw new ArgumentException($"Unknown operation: {request.Operation}")
        };
    }

    /// <summary>
    /// Method that returns complex status data
    /// </summary>
    public ClientStatus GetStatus()
    {
        _logger.LogInformation("GetStatus invoked");

        return new ClientStatus
        {
            IsConnected = true,
            ConnectionQuality = "Good",
            LastActivity = DateTime.Now,
            MemoryUsage = Random.Shared.Next(50, 150),
            ActiveFeatures = new List<string> { "SignalR", "UserManagement", "Communicator" }
        };
    }

    /// <summary>
    /// Async method that processes data
    /// </summary>
    public async Task<string> ProcessDataAsync(ProcessRequest request)
    {
        _logger.LogInformation("ProcessDataAsync invoked with data: {Data}", request.Data);

        // Simulate some processing
        await Task.Delay(100);

        var processed = request.Data?.ToUpper() ?? "NO DATA";
        var result = $"Processed: {processed} at {DateTime.Now:HH:mm:ss}";

        _logger.LogInformation("ProcessDataAsync completed: {Result}", result);
        return result;
    }

    /// <summary>
    /// Simulates a long-running operation with configurable delay
    /// </summary>
    public async Task<string> SimulateDelayAsync(DelayRequest request)
    {
        var delayMs = Math.Clamp(request.DelayMilliseconds, 0, 5000);
        _logger.LogInformation("SimulateDelayAsync invoked with {Delay}ms delay", delayMs);

        await Task.Delay(delayMs);

        var result = $"Completed after {delayMs}ms delay at {DateTime.Now:HH:mm:ss}";
        _logger.LogInformation("SimulateDelayAsync completed");
        return result;
    }
}

// ============================================================================
// Request/Response Models
// ============================================================================

public class PingResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ClientType { get; set; } = string.Empty;
}

public class ClientInfo
{
    public string ClientType { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserAgent { get; set; } = string.Empty;
}

public class CalculationRequest
{
    public int A { get; set; }
    public int B { get; set; }
    public string Operation { get; set; } = "add";
}

public class ClientStatus
{
    public bool IsConnected { get; set; }
    public string ConnectionQuality { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public int MemoryUsage { get; set; }
    public List<string> ActiveFeatures { get; set; } = new();
}

public class ProcessRequest
{
    public string Data { get; set; } = string.Empty;
}

public class DelayRequest
{
    public int DelayMilliseconds { get; set; }
}
