using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GPMH.Infrastructure;

/// <summary>
/// Opt-in Development-only database initializer used when running locally against SQLite.
/// Never runs in Staging/Production and seeds demo users only when DevSeed:Password is supplied at runtime.
/// </summary>
public static class DevDatabaseInitializer
{
    public static async Task InitializeDevelopmentDatabaseAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        string? seedPassword = configuration["DevSeed:Password"];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        if (await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IPasswordHasherService passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        IDefaultFormulaSeeder formulaSeeder = scope.ServiceProvider.GetRequiredService<IDefaultFormulaSeeder>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        DateTimeOffset now = clock.UtcNow;

        Tenant demoTenant = new()
        {
            TenantCode = "sonimoha-171391",
            LgdCode = "171391",
            NameEn = "Sonimoha Gram Panchayat",
            NameMr = "सोनीमोहा ग्रामपंचायत",
            Status = TenantStatus.Active,
            SubscriptionExpiresAtUtc = now.AddYears(1),
        };
        demoTenant.Activate(now);

        TenantDatabaseMapping mapping = new()
        {
            TenantId = demoTenant.Id,
            IsolationTier = "SHARED",
            SchemaName = "public",
            IsActive = true,
        };

        User superAdmin = new()
        {
            Username = "superadmin",
            Email = "superadmin@gpmh.local",
            FullNameEn = "Platform Super Admin",
            FullNameMr = "प्लॅटफॉर्म सुपर अ‍ॅडमिन",
            PasswordHash = passwordHasher.HashPassword(seedPassword),
            IsSuperAdmin = true,
            IsActive = true,
        };

        User gramsevak = new()
        {
            Username = "gramsevak",
            Email = "gramsevak@sonimoha.local",
            FullNameEn = "Suresh Deshmukh",
            FullNameMr = "सुरेश देशमुख",
            PasswordHash = passwordHasher.HashPassword(seedPassword),
            IsSuperAdmin = false,
            IsActive = true,
        };

        TenantMembership membership = new()
        {
            UserId = gramsevak.Id,
            TenantId = demoTenant.Id,
            Role = ApplicationRole.Gramsevak,
            IsDefault = true,
            IsActive = true,
        };

        try
        {
            await db.Tenants.AddAsync(demoTenant, cancellationToken).ConfigureAwait(false);
            await db.TenantDatabaseMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
            await db.Users.AddRangeAsync([superAdmin, gramsevak], cancellationToken).ConfigureAwait(false);
            await db.TenantMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

            tenantContext.SetTenant(demoTenant.Id);

            await db.GramPanchayats.AddAsync(new GramPanchayat
            {
                TenantId = demoTenant.Id,
                LgdCode = "171391",
                DistrictCode = "497",
                DistrictName = "Beed",
                TehsilCode = "4192",
                TehsilName = "Dharur",
                PanchayatNameEn = "Sonimoha",
                PanchayatNameMr = "सोनीमोहा",
                Pincode = "431124",
                SarpanchName = "Sunita Kale",
                GramsevakName = "Suresh Deshmukh",
                TotalWards = 4,
                PopulationCensus = 2480,
                SetupProgressPct = 100,
                IsArrearsRolloverInitialized = false,
            }, cancellationToken).ConfigureAwait(false);

            await db.AssessmentYears.AddAsync(new AssessmentYear
            {
                TenantId = demoTenant.Id,
                YearLabel = "2026-27",
                StartYear = 2026,
                EndYear = 2027,
                IsActive = true,
                IsLocked = false,
            }, cancellationToken).ConfigureAwait(false);

            await db.BankAccounts.AddAsync(new BankAccount
            {
                TenantId = demoTenant.Id,
                FundType = "GRAM_NIDHI",
                BankName = "Bank of Maharashtra",
                BranchName = "Dharur",
                AccountNumber = "XXXXXXXX4321",
                IfscCode = "MAHB0000412",
                UpiId = "sonimohagp@mahabank",
                UpiPayeeName = "Sonimoha Gram Panchayat",
                OpeningBalance = 125000.00m,
                IsDefault = true,
                IsActive = true,
            }, cancellationToken).ConfigureAwait(false);

            await db.Properties.AddAsync(new Property
            {
                TenantId = demoTenant.Id,
                WardNo = "W-01",
                PropertyNo = "101",
                BuiltUpAreaSqFt = 450.00m,
                OpenPlotAreaSqFt = 120.00m,
                TotalAreaSqFt = 570.00m,
                WaterConnectionCode = "HALF_INCH",
            }, cancellationToken).ConfigureAwait(false);

            await db.Citizens.AddAsync(new Citizen
            {
                TenantId = demoTenant.Id,
                CitizenCode = "CIT-101",
                WardNo = "W-01",
                FullNameEn = "Rameshwar Patil",
                FullNameMr = "रामेश्वर पाटील",
                MobileNo = "9822001122",
                Gender = "Male",
                AadhaarLast4 = "4567",
            }, cancellationToken).ConfigureAwait(false);

            await formulaSeeder.SeedDefaultsForTenantAsync(demoTenant.Id, cancellationToken).ConfigureAwait(false);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            tenantContext.Clear();
        }
    }
}

