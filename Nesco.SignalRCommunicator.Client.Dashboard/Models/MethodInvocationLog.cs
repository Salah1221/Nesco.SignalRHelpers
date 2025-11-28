namespace Nesco.SignalRCommunicator.Client.Dashboard.Models;

/// <summary>
/// Log entry for a method invocation received from the server
/// </summary>
public class MethodInvocationLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string MethodName { get; set; } = string.Empty;
    public string? Parameter { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}
