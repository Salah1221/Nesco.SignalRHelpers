using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nesco.SignalRCommunicator.Dashboard.Services;

namespace Nesco.SignalRCommunicator.Dashboard;

/// <summary>
/// Extension methods for registering SignalR Dashboard services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SignalR Dashboard service to the service collection.
    /// Requires that SignalRCommunicator and SignalRUserManagement services are already registered.
    /// </summary>
    /// <typeparam name="TUser">The IdentityUser type used in your application</typeparam>
    /// <typeparam name="TDbContext">The DbContext type that contains Connections and ConnectedUsers DbSets</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSignalRDashboard<TUser, TDbContext>(this IServiceCollection services)
        where TUser : IdentityUser
        where TDbContext : DbContext
    {
        // Register the dashboard service
        services.AddScoped<IDashboardService, DashboardService<TUser, TDbContext>>();

        return services;
    }
}
