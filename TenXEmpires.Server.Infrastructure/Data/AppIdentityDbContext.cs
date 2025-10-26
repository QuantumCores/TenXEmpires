using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TenXEmpires.Server.Infrastructure.Data;

/// <summary>
/// ASP.NET Core Identity DbContext mapped to the 'auth' schema.
/// </summary>
public class AppIdentityDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Store Identity tables in the 'auth' schema to match migrations
        builder.HasDefaultSchema("auth");
    }
}

