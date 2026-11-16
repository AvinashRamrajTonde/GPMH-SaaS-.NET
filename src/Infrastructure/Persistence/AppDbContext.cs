using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Taxation;
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
    public DbSet<TenantSubscriptionPayment> TenantSubscriptionPayments => Set<TenantSubscriptionPayment>();
    public DbSet<ProtectedAsset> ProtectedAssets => Set<ProtectedAsset>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GramPanchayat> GramPanchayats => Set<GramPanchayat>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<AssessmentYear> AssessmentYears => Set<AssessmentYear>();
    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<FormulaSetting> FormulaSettings => Set<FormulaSetting>();
    public DbSet<ConstructionTypeMaster> ConstructionTypeMasters => Set<ConstructionTypeMaster>();
    public DbSet<UsageWeightage> UsageWeightages => Set<UsageWeightage>();
    public DbSet<DepreciationSlab> DepreciationSlabs => Set<DepreciationSlab>();
    public DbSet<AreaSlabTax> AreaSlabTaxes => Set<AreaSlabTax>();
    public DbSet<WaterTaxMaster> WaterTaxMasters => Set<WaterTaxMaster>();
    public DbSet<FieldAgent> FieldAgents => Set<FieldAgent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        ConfigureTenant(modelBuilder);
        ConfigureTenantDatabaseMapping(modelBuilder);
        ConfigureTenantSubscriptionPayment(modelBuilder);
        ConfigureProtectedAsset(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureGramPanchayat(modelBuilder);
        ConfigureBankAccount(modelBuilder);
        ConfigureTenantMembership(modelBuilder);
        ConfigureAssessmentYear(modelBuilder);
        ConfigureCitizen(modelBuilder);
        ConfigureProperty(modelBuilder);
        ConfigureTaxationMasters(modelBuilder);
        ConfigureFieldAgent(modelBuilder);
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
            entity.Property(e => e.CensusCode).HasMaxLength(32);
            entity.Property(e => e.OfficeAddress).HasMaxLength(500);
            entity.Property(e => e.SarpanchName).HasMaxLength(200);
            entity.Property(e => e.SarpanchMobile).HasMaxLength(20);
            entity.Property(e => e.UpSarpanchName).HasMaxLength(200);
            entity.Property(e => e.GramsevakName).HasMaxLength(200);
            entity.Property(e => e.GramsevakMobile).HasMaxLength(20);
            entity.Property(e => e.OperatorName).HasMaxLength(200);
            entity.Property(e => e.OperatorMobile).HasMaxLength(20);
            entity.Property(e => e.GramsevakSignatureKey).HasMaxLength(128);
            entity.Property(e => e.SarpanchSignatureKey).HasMaxLength(128);
            entity.Property(e => e.OperatorSignatureKey).HasMaxLength(128);
            entity.Property(e => e.OfficialStampKey).HasMaxLength(128);
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

    private static void ConfigureTenantSubscriptionPayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantSubscriptionPayment>(entity =>
        {
            entity.ToTable("tenant_subscription_payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlanName).HasMaxLength(40).IsRequired();
            entity.Property(e => e.PaymentMode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(14, 2);
            entity.Property(e => e.PaymentReference).HasMaxLength(120);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.PaymentDateUtc });
        });
    }

    private static void ConfigureProtectedAsset(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProtectedAsset>(entity =>
        {
            entity.ToTable("protected_assets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssetKey).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(32).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(120).IsRequired();
            entity.HasIndex(e => e.AssetKey).IsUnique();
        });
    }

    private void ConfigureBankAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FundType).HasMaxLength(40).IsRequired();
            entity.Property(e => e.BankName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.BranchName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AccountNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.IfscCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.UpiId).HasMaxLength(120);
            entity.Property(e => e.UpiPayeeName).HasMaxLength(150);
            entity.Property(e => e.QrImageKey).HasMaxLength(128);
            entity.Property(e => e.OpeningBalance).HasPrecision(14, 2);
            entity.HasIndex(e => new { e.TenantId, e.FundType }).IsUnique();

            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });
    }

    private void ConfigureTaxationMasters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormulaSetting>(entity =>
        {
            entity.ToTable("formula_settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RatePeriodName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.TaxDivisor).HasPrecision(10, 2);
            entity.Property(e => e.EarlyRebatePct).HasPrecision(5, 2);
            entity.Property(e => e.LatePenaltyPct).HasPrecision(5, 2);
            entity.Property(e => e.LastResolutionNo).HasMaxLength(80);
            entity.Property(e => e.LastChangeReason).HasMaxLength(400);
            entity.HasIndex(e => new { e.TenantId, e.RatePeriodName }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ConstructionTypeMaster>(entity =>
        {
            entity.ToTable("construction_type_masters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(40).IsRequired();
            entity.Property(e => e.TypeName).HasMaxLength(160).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(40).IsRequired();
            entity.Property(e => e.LandAnnualValueRate).HasPrecision(10, 4);
            entity.Property(e => e.ConstructionReadyReckonerRate).HasPrecision(12, 4);
            entity.Property(e => e.UsageWeightageFactor).HasPrecision(8, 4);
            entity.Property(e => e.TaxRate).HasPrecision(8, 4);
            entity.Property(e => e.LastResolutionNo).HasMaxLength(80);
            entity.Property(e => e.LastChangeReason).HasMaxLength(400);
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UsageWeightage>(entity =>
        {
            entity.ToTable("usage_weightages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
            entity.Property(e => e.UsageName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Weightage).HasPrecision(8, 4);
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DepreciationSlab>(entity =>
        {
            entity.ToTable("depreciation_slabs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HutMudPercent).HasPrecision(5, 2);
            entity.Property(e => e.StoneBrickRccPercent).HasPrecision(5, 2);
            entity.HasIndex(e => new { e.TenantId, e.AgeFrom, e.AgeTo }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AreaSlabTax>(entity =>
        {
            entity.ToTable("area_slab_taxes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LightingTax).HasPrecision(14, 2);
            entity.Property(e => e.HealthTax).HasPrecision(14, 2);
            entity.Property(e => e.SanitationTax).HasPrecision(14, 2);
            entity.Property(e => e.ToiletTax).HasPrecision(14, 2);
            entity.HasIndex(e => new { e.TenantId, e.AreaFrom, e.AreaTo }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<WaterTaxMaster>(entity =>
        {
            entity.ToTable("water_tax_masters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(32).IsRequired();
            entity.Property(e => e.WaterTypeName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(14, 2);
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasQueryFilter(e =>
                tenantContext.CurrentTenantId != null &&
                (Guid?)e.TenantId == tenantContext.CurrentTenantId);
        });
    }

    private static void ConfigureFieldAgent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FieldAgent>(entity =>
        {
            entity.ToTable("field_agents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.StatusReason).HasMaxLength(300);
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.MobileNo).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IdProofType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.PanMasked).HasMaxLength(16).IsRequired();
            entity.Property(e => e.AadhaarMasked).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AadhaarBlindIndex).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PanBlindIndex).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(400).IsRequired();
            entity.Property(e => e.VillageOrCity).HasMaxLength(150).IsRequired();
            entity.Property(e => e.District).HasMaxLength(120).IsRequired();
            entity.Property(e => e.TalukasCsv).HasMaxLength(400).IsRequired();
            entity.Property(e => e.State).HasMaxLength(80).IsRequired();
            entity.Property(e => e.BankName).HasMaxLength(150);
            entity.Property(e => e.BankBranch).HasMaxLength(150);
            entity.Property(e => e.AccountNumber).HasMaxLength(64);
            entity.Property(e => e.IfscCode).HasMaxLength(20);
            entity.Property(e => e.UpiId).HasMaxLength(120);
            entity.Property(e => e.AadhaarDocumentKey).HasMaxLength(128);
            entity.Property(e => e.PanDocumentKey).HasMaxLength(128);
            entity.Property(e => e.RequestedCommissionRatePct).HasPrecision(5, 2);
            entity.Property(e => e.ApprovedNewGpRatePct).HasPrecision(5, 2);
            entity.Property(e => e.ApprovedRenewalRatePct).HasPrecision(5, 2);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.AgentCode).IsUnique();
            entity.HasIndex(e => e.MobileNo).IsUnique();
            entity.HasIndex(e => e.AadhaarBlindIndex).IsUnique();
            entity.HasIndex(e => e.PanBlindIndex).IsUnique();
        });
    }
}
