using Nesco.SignalRCommunicator.Client.Dashboard.Models;

namespace Nesco.SignalRCommunicator.Client.Dashboard.Services;

/// <summary>
/// Implementation of method invocation logger
/// </summary>
public class MethodInvocationLogger : IMethodInvocationLogger
{
    private readonly List<MethodInvocationLog> _logs = new();
    private readonly object _lock = new();

    public event Action? OnLogAdded;

    public int MaxEntries { get; } = 50;

    public void Log(string methodName, string? parameter, string? result, string? error, bool success, TimeSpan duration)
    {
        lock (_lock)
        {
            _logs.Insert(0, new MethodInvocationLog
            {
                Timestamp = DateTime.Now,
                MethodName = methodName,
                Parameter = parameter,
                Result = result,
                Error = error,
                Success = success,
                Duration = duration
            });

            // Trim to max entries
            while (_logs.Count > MaxEntries)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }

        OnLogAdded?.Invoke();
    }

    public IEnumerable<MethodInvocationLog> GetLogs()
    {
        lock (_lock)
        {
            return _logs.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }

        OnLogAdded?.Invoke();
    }
}
