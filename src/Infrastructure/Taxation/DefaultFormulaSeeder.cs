using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Taxation;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Taxation;

/// <summary>
/// Canonical ADR-016 Maharashtra statutory formula and master data seeder matching docs/statutory/statutory-master-seeds.json.
/// </summary>
public sealed class DefaultFormulaSeeder(AppDbContext dbContext) : IDefaultFormulaSeeder
{
    public async Task SeedDefaultsForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        bool alreadySeeded = await dbContext.FormulaSettings
            .IgnoreQueryFilters()
            .AnyAsync(f => f.TenantId == tenantId && f.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (alreadySeeded)
        {
            return;
        }

        await dbContext.FormulaSettings.AddAsync(new FormulaSetting
        {
            TenantId = tenantId,
            RatePeriodName = "2026-27 ते 2029-30",
            RateStartYear = 2026,
            RateEndYear = 2029,
            TaxDivisor = 1000m,
            NoticeGraceDays = 15,
            EarlyRebatePct = 10.0m,
            LatePenaltyPct = 10.0m,
            ApplyLightingHealthOnOpenPlot = true,
            RoundArea = true,
            IsActive = true,
        }, cancellationToken).ConfigureAwait(false);

        await dbContext.ConstructionTypeMasters.AddRangeAsync(
            CreateConstructionTypes(tenantId),
            cancellationToken).ConfigureAwait(false);

        await dbContext.UsageWeightages.AddRangeAsync(
            CreateUsageWeightages(tenantId),
            cancellationToken).ConfigureAwait(false);

        await dbContext.DepreciationSlabs.AddRangeAsync(
            CreateDepreciationSlabs(tenantId),
            cancellationToken).ConfigureAwait(false);

        await dbContext.AreaSlabTaxes.AddRangeAsync(
            CreateAreaSlabTaxes(tenantId),
            cancellationToken).ConfigureAwait(false);

        await dbContext.WaterTaxMasters.AddRangeAsync(
            CreateWaterTaxes(tenantId),
            cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<ConstructionTypeMaster> CreateConstructionTypes(Guid tenantId) =>
    [
        NewConstruction(tenantId, "HUT_MUD", "झोपडी किंवा मातीची इमारत", "HUT_MUD", 760m, 5852m, 1m, 0.5m),
        NewConstruction(tenantId, "STONE_BRICK_MUD", "दगड विटा मातीची इमारत", "STONE_BRICK_RCC", 760m, 10032m, 1m, 0.8m),
        NewConstruction(tenantId, "STONE_LIME_SEMI_PUCCA", "दगड सिमेंट चुना अर्ध पक्के घर", "STONE_BRICK_RCC", 758m, 14012m, 1m, 1.0m),
        NewConstruction(tenantId, "RCC_PUCCA", "आर सी सी पक्के घर", "STONE_BRICK_RCC", 760m, 18392m, 1m, 1.5m),
        NewConstruction(tenantId, "VACANT_SPACE", "खाली जागा", "OPEN_LAND", 760m, 2m, 0m, 2.0m),
        NewConstruction(tenantId, "TOWER", "मनोरे", "SPECIAL", 760m, 0m, 1m, 8.0m),
        NewConstruction(tenantId, "TOWER_OPEN_SPACE", "मनोऱ्याची खुली जागा", "OPEN_LAND", 760m, 0m, 0m, 2.0m),
        NewConstruction(tenantId, "CATTLE_SHED", "जनावरांचा गोठा", "HUT_MUD", 760m, 5852m, 1m, 0.5m),
        NewConstruction(tenantId, "TIN_SHED", "पत्र्याचे शेड", "HUT_MUD", 760m, 5852m, 1m, 0.5m),
        NewConstruction(tenantId, "BAKHAL_SPACE", "बखळ जागा", "OPEN_LAND", 760m, 0m, 0m, 2.0m),
        NewConstruction(tenantId, "OPEN_PLOT", "खुली जागा", "OPEN_LAND", 760m, 0m, 0m, 2.0m),
        NewConstruction(tenantId, "OTHER_COMMERCIAL", "इतर व्यापारी / गोदाम बांधकाम", "STONE_BRICK_RCC", 760m, 18392m, 1m, 1.5m),
    ];

    public static IReadOnlyList<UsageWeightage> CreateUsageWeightages(Guid tenantId) =>
    [
        new() { TenantId = tenantId, Code = "RESIDENTIAL", UsageName = "निवासी", Weightage = 1.0m },
        new() { TenantId = tenantId, Code = "INDUSTRIAL", UsageName = "औद्योगिक", Weightage = 1.2m },
        new() { TenantId = tenantId, Code = "COMMERCIAL", UsageName = "वाणिज्य", Weightage = 1.25m },
    ];

    public static IReadOnlyList<DepreciationSlab> CreateDepreciationSlabs(Guid tenantId) =>
    [
        new() { TenantId = tenantId, AgeFrom = 0, AgeTo = 2, FromInclusive = true, ToInclusive = true, HutMudPercent = 100m, StoneBrickRccPercent = 100m },
        new() { TenantId = tenantId, AgeFrom = 2, AgeTo = 5, FromInclusive = false, ToInclusive = true, HutMudPercent = 95m, StoneBrickRccPercent = 95m },
        new() { TenantId = tenantId, AgeFrom = 5, AgeTo = 10, FromInclusive = false, ToInclusive = true, HutMudPercent = 85m, StoneBrickRccPercent = 90m },
        new() { TenantId = tenantId, AgeFrom = 10, AgeTo = 20, FromInclusive = false, ToInclusive = true, HutMudPercent = 75m, StoneBrickRccPercent = 80m },
        new() { TenantId = tenantId, AgeFrom = 20, AgeTo = 30, FromInclusive = false, ToInclusive = true, HutMudPercent = 60m, StoneBrickRccPercent = 70m },
        new() { TenantId = tenantId, AgeFrom = 30, AgeTo = 40, FromInclusive = false, ToInclusive = true, HutMudPercent = 45m, StoneBrickRccPercent = 60m },
        new() { TenantId = tenantId, AgeFrom = 40, AgeTo = 50, FromInclusive = false, ToInclusive = true, HutMudPercent = 30m, StoneBrickRccPercent = 50m },
        new() { TenantId = tenantId, AgeFrom = 50, AgeTo = 60, FromInclusive = false, ToInclusive = true, HutMudPercent = 20m, StoneBrickRccPercent = 40m },
        new() { TenantId = tenantId, AgeFrom = 60, AgeTo = 100, FromInclusive = false, ToInclusive = true, HutMudPercent = 15m, StoneBrickRccPercent = 30m },
    ];

    public static IReadOnlyList<AreaSlabTax> CreateAreaSlabTaxes(Guid tenantId) =>
    [
        new() { TenantId = tenantId, AreaFrom = 1, AreaTo = 300, LightingTax = 40m, HealthTax = 40m, SanitationTax = 100m, ToiletTax = 0m },
        new() { TenantId = tenantId, AreaFrom = 301, AreaTo = 700, LightingTax = 50m, HealthTax = 50m, SanitationTax = 100m, ToiletTax = 20m },
        new() { TenantId = tenantId, AreaFrom = 701, AreaTo = 999999, LightingTax = 100m, HealthTax = 100m, SanitationTax = 100m, ToiletTax = 0m },
    ];

    public static IReadOnlyList<WaterTaxMaster> CreateWaterTaxes(Guid tenantId) =>
    [
        new() { TenantId = tenantId, Code = "NONE", WaterTypeName = "नाही", Amount = 0m },
        new() { TenantId = tenantId, Code = "SPECIAL", WaterTypeName = "विशेष पाणी कर", Amount = 720m },
        new() { TenantId = tenantId, Code = "GENERAL", WaterTypeName = "सामान्य पाणी कर", Amount = 720m },
    ];

    private static ConstructionTypeMaster NewConstruction(
        Guid tenantId,
        string code,
        string typeName,
        string category,
        decimal landRate,
        decimal rrRate,
        decimal weightFactor,
        decimal taxRate) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            TypeName = typeName,
            Category = category,
            LandAnnualValueRate = landRate,
            ConstructionReadyReckonerRate = rrRate,
            UsageWeightageFactor = weightFactor,
            TaxRate = taxRate,
        };
}

