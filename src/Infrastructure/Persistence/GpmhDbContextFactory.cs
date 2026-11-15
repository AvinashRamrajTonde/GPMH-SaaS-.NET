using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GPMH.Infrastructure.Persistence;

/// <summary>
/// Enables offline `dotnet ef migrations add` without booting WebAPI or requiring a live DB server.
/// </summary>
public sealed class GpmhDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AppDbContext> builder = new();
        builder.UseNpgsql(
            "Host=localhost;Port=5432;Database=gpmh_design;Username=gpmh_app;Password=design_only",
            npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(builder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? CurrentTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void Clear() { }
    }
}

