using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BlazorServerApplication.Services;

/// <summary>
/// Custom user ID provider for SignalR that extracts user ID from JWT claims
/// </summary>
public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Try to get user ID from different claim types
        var userId = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? connection.User?.FindFirst(ClaimTypes.Name)?.Value
                     ?? connection.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            Console.WriteLine($">>> [CustomUserIdProvider] Extracted UserId: {userId} for ConnectionId: {connection.ConnectionId}");
        }
        else
        {
            Console.WriteLine($">>> [CustomUserIdProvider] WARNING: No UserId found for ConnectionId: {connection.ConnectionId}. User authenticated: {connection.User?.Identity?.IsAuthenticated}");

            // Log all available claims for debugging
            if (connection.User?.Claims != null)
            {
                Console.WriteLine($">>> [CustomUserIdProvider] Available claims:");
                foreach (var claim in connection.User.Claims)
                {
                    Console.WriteLine($">>>   - {claim.Type}: {claim.Value}");
                }
            }
        }

        return userId;
    }
}
