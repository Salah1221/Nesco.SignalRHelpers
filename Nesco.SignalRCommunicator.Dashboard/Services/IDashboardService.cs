using Nesco.SignalRCommunicator.Core.Models;
using Nesco.SignalRCommunicator.Dashboard.Models;

namespace Nesco.SignalRCommunicator.Dashboard.Services;

/// <summary>
/// Service that coordinates SignalRUserManagement and SignalRCommunicator for the dashboard
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Invokes a method on all connected users
    /// </summary>
    Task<SignalRResponse> InvokeOnAllConnectedAsync(string methodName, object? parameter);
    Task<T?> InvokeOnAllConnectedAsync<T>(string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a specific user (all their connections)
    /// </summary>
    Task<SignalRResponse> InvokeOnUserAsync(string userId, string methodName, object? parameter);
    Task<T?> InvokeOnUserAsync<T>(string userId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on multiple users (all their connections)
    /// </summary>
    Task<SignalRResponse> InvokeOnUsersAsync(IEnumerable<string> userIds, string methodName, object? parameter);
    Task<T?> InvokeOnUsersAsync<T>(IEnumerable<string> userIds, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Invokes a method on a specific connection
    /// </summary>
    Task<SignalRResponse> InvokeOnConnectionAsync(string connectionId, string methodName, object? parameter);
    Task<T?> InvokeOnConnectionAsync<T>(string connectionId, string methodName, object? parameter) where T : class;

    /// <summary>
    /// Gets count of connected users
    /// </summary>
    int GetConnectedUsersCount();

    /// <summary>
    /// Gets count of active connections
    /// </summary>
    int GetActiveConnectionsCount();

    /// <summary>
    /// Checks if a user is connected
    /// </summary>
    bool IsUserConnected(string userId);

    /// <summary>
    /// Gets all connected users with their connections
    /// </summary>
    Task<List<ConnectedUserInfo>> GetConnectedUsersAsync();
}
