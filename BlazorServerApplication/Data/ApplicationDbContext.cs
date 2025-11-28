using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nesco.SignalRUserManagement.Server.Models;

namespace BlazorServerApplication.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public virtual DbSet<ConnectedUser> ConnectedUsers { get; set; }
    public virtual DbSet<Connection> Connections { get; set; }
}