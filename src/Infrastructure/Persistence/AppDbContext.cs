using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDatabaseMapping> TenantDatabaseMappings => Set<TenantDatabaseMapping>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GramPanchayat> GramPanchayats => Set<GramPanchayat>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<AssessmentYear> AssessmentYears => Set<AssessmentYear>();
    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        ConfigureTenant(modelBuilder);
        ConfigureTenantDatabaseMapping(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureGramPanchayat(modelBuilder);
        ConfigureTenantMembership(modelBuilder);
        ConfigureAssessmentYear(modelBuilder);
        ConfigureCitizen(modelBuilder);
        ConfigureProperty(modelBuilder);
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

    private static void ConfigureTenantDatabaseMapping(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantDatabaseMapping>(entity =>
        {
            entity.ToTable("tenant_database_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsolationTier).HasMaxLength(24).IsRequired();
            entity.Property(e => e.SchemaName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ConnectionSecretName).HasMaxLength(128);
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.HasOne<Tenant>()
                .WithOne()
                .HasForeignKey<TenantDatabaseMapping>(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.FullNameMr).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FullNameEn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }

    private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RevokedReason).HasMaxLength(120);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.FamilyId);
            entity.HasIndex(e => new { e.UserId, e.ExpiresAtUtc });
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.Pincode).HasMaxLength(10);
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

    private void ConfigureCitizen(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Citizen>(entity =>
        {
            entity.ToTable("citizens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CitizenCode).HasMaxLength(40).IsRequired();
            entity.Property(e => e.WardNo).HasMaxLength(40);
            entity.Property(e => e.FullNameMr).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FullNameEn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Gender).HasMaxLength(16).IsRequired();
            entity.Property(e => e.MobileNo).HasMaxLength(20);
            entity.Property(e => e.AadhaarBlindIndex).HasMaxLength(64);
            entity.Property(e => e.AadhaarLast4).HasMaxLength(4);
            entity.Property(e => e.SocialCategory).HasMaxLength(24).IsRequired();
            entity.Property(e => e.ExemptionCategory).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.CitizenCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AadhaarBlindIndex }).IsUnique();

            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });
    }

    private void ConfigureProperty(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WardNo).HasMaxLength(40).IsRequired();
            entity.Property(e => e.PropertyNo).HasMaxLength(40).IsRequired();
            entity.Property(e => e.BuiltUpAreaSqFt).HasPrecision(12, 4);
            entity.Property(e => e.OpenPlotAreaSqFt).HasPrecision(12, 4);
            entity.Property(e => e.TotalAreaSqFt).HasPrecision(12, 4);
            entity.Property(e => e.BoundaryEast).HasMaxLength(300);
            entity.Property(e => e.BoundaryWest).HasMaxLength(300);
            entity.Property(e => e.BoundaryNorth).HasMaxLength(300);
            entity.Property(e => e.BoundarySouth).HasMaxLength(300);
            entity.Property(e => e.WaterConnectionCode).HasMaxLength(24).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WardNo, e.PropertyNo }).IsUnique();
            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(e => e.ParentPropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e =>
                !e.IsDeleted &&
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
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.HasIndex(e => new { e.TenantId, e.TimestampUtc });
            entity.HasIndex(e => new { e.EntityName, e.EntityId, e.TimestampUtc });
        });
    }
}
