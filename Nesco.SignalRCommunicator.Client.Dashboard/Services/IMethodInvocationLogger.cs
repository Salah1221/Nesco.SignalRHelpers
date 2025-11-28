using Nesco.SignalRCommunicator.Client.Dashboard.Models;

namespace Nesco.SignalRCommunicator.Client.Dashboard.Services;

/// <summary>
/// Service for logging method invocations from the server
/// </summary>
public interface IMethodInvocationLogger
{
    /// <summary>
    /// Event fired when a new log entry is added
    /// </summary>
    event Action? OnLogAdded;

    /// <summary>
    /// Logs a method invocation
    /// </summary>
    void Log(string methodName, string? parameter, string? result, string? error, bool success, TimeSpan duration);

    /// <summary>
    /// Gets all log entries (most recent first)
    /// </summary>
    IEnumerable<MethodInvocationLog> GetLogs();

    /// <summary>
    /// Clears all log entries
    /// </summary>
    void Clear();

    /// <summary>
    /// Maximum number of log entries to keep
    /// </summary>
    int MaxEntries { get; }
}
