using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorWebassemblyApp.Services;

/// <summary>
/// Service that logs method invocations from the server for display in the UI
/// </summary>
public class MethodInvocationLogger
{
    private readonly ConcurrentQueue<MethodInvocationLog> _logs = new();
    private const int MaxLogEntries = 50;

    /// <summary>
    /// Event raised when a new log entry is added
    /// </summary>
    public event Action? OnLogAdded;

    /// <summary>
    /// Logs a method invocation
    /// </summary>
    public void LogInvocation(string methodName, object? parameter, object? result, TimeSpan duration, Exception? error = null)
    {
        var log = new MethodInvocationLog
        {
            Timestamp = DateTime.Now,
            MethodName = methodName,
            Parameter = parameter != null ? JsonSerializer.Serialize(parameter, new JsonSerializerOptions { WriteIndented = false }) : null,
            Result = result != null ? JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }) : null,
            Duration = duration,
            Success = error == null,
            Error = error?.Message
        };

        _logs.Enqueue(log);

        // Keep only the last MaxLogEntries entries
        while (_logs.Count > MaxLogEntries)
        {
            _logs.TryDequeue(out _);
        }

        OnLogAdded?.Invoke();
    }

    /// <summary>
    /// Gets all log entries
    /// </summary>
    public IEnumerable<MethodInvocationLog> GetLogs()
    {
        return _logs.Reverse();
    }

    /// <summary>
    /// Clears all log entries
    /// </summary>
    public void Clear()
    {
        _logs.Clear();
        OnLogAdded?.Invoke();
    }
}

/// <summary>
/// Represents a single method invocation log entry
/// </summary>
public class MethodInvocationLog
{
    public DateTime Timestamp { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string? Parameter { get; set; }
    public string? Result { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
