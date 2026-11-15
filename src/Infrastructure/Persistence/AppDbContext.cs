using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<GramPanchayat> GramPanchayats => Set<GramPanchayat>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<AssessmentYear> AssessmentYears => Set<AssessmentYear>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        ConfigureTenant(modelBuilder);
        ConfigureGramPanchayat(modelBuilder);
        ConfigureTenantMembership(modelBuilder);
        ConfigureAssessmentYear(modelBuilder);
        ConfigureAuditLog(modelBuilder);
    }

    private static void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.LgdCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.NameEn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.NameMr).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.TenantCode).IsUnique();
            entity.HasIndex(e => e.LgdCode).IsUnique();
        });
    }

    private void ConfigureGramPanchayat(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GramPanchayat>(entity =>
        {
            entity.ToTable("gram_panchayats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LgdCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DistrictCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DistrictName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.TehsilCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.TehsilName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PanchayatNameEn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PanchayatNameMr).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UpiId).HasMaxLength(120);
            entity.Property(e => e.UpiPayeeName).HasMaxLength(150);
            entity.Property(e => e.BankAccountLast4).HasMaxLength(8);
            entity.HasIndex(e => new { e.TenantId, e.LgdCode }).IsUnique();

            // Fail-closed without touching Nullable.Value: unresolved tenant context yields zero rows.
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });
    }

    private static void ConfigureTenantMembership(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("tenant_memberships");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
        });
    }

    private void ConfigureAssessmentYear(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssessmentYear>(entity =>
        {
            entity.ToTable("assessment_years");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.YearLabel).HasMaxLength(16).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.YearLabel }).IsUnique();

            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ActorId).HasMaxLength(64);
            entity.Property(e => e.ActorRole).HasMaxLength(64);
            entity.Property(e => e.CorrelationId).HasMaxLength(120);
            entity.HasIndex(e => new { e.TenantId, e.TimestampUtc });
        });
    }
}
