using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Domain.Entities.Taxation;
using GPMH.Infrastructure.Taxation;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class FormulaMasterRepository(
    AppDbContext dbContext,
    IDefaultFormulaSeeder defaultFormulaSeeder)
    : IFormulaMasterRepository
{
    public async Task<IReadOnlyList<FormulaSetting>> ListFormulaSettingsAsync(CancellationToken cancellationToken) =>
        await dbContext.FormulaSettings
            .OrderByDescending(f => f.IsActive)
            .ThenByDescending(f => f.RateStartYear)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken) =>
        dbContext.FormulaSettings
            .OrderByDescending(f => f.IsActive)
            .ThenByDescending(f => f.RateStartYear)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<FormulaSetting?> GetFormulaSettingByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.FormulaSettings.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<FormulaSetting?> GetFormulaSettingByPeriodNameAsync(
        string ratePeriodName,
        CancellationToken cancellationToken) =>
        dbContext.FormulaSettings.FirstOrDefaultAsync(
            f => f.RatePeriodName == ratePeriodName,
            cancellationToken);

    public async Task<IReadOnlyList<ConstructionTypeMaster>> ListConstructionTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.ConstructionTypeMasters
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ConstructionTypeMaster?> GetConstructionTypeByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ConstructionTypeMasters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<ConstructionTypeMaster?> GetConstructionTypeByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        dbContext.ConstructionTypeMasters.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<FormulaWorkspaceSummaryDto> GetWorkspaceSummaryCountsAsync(CancellationToken cancellationToken)
    {
        int constructionTypes = await dbContext.ConstructionTypeMasters.CountAsync(cancellationToken).ConfigureAwait(false);
        int usageWeightages = await dbContext.UsageWeightages.CountAsync(cancellationToken).ConfigureAwait(false);
        int depreciationSlabs = await dbContext.DepreciationSlabs.CountAsync(cancellationToken).ConfigureAwait(false);
        int areaSlabTaxes = await dbContext.AreaSlabTaxes.CountAsync(cancellationToken).ConfigureAwait(false);
        int waterTaxes = await dbContext.WaterTaxMasters.CountAsync(cancellationToken).ConfigureAwait(false);
        int ratePeriods = await dbContext.FormulaSettings.CountAsync(cancellationToken).ConfigureAwait(false);

        return new FormulaWorkspaceSummaryDto(
            constructionTypes,
            usageWeightages,
            depreciationSlabs,
            areaSlabTaxes,
            waterTaxes,
            ratePeriods);
    }

    public async Task AddFormulaSettingAsync(FormulaSetting entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await dbContext.FormulaSettings.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddConstructionTypeAsync(ConstructionTypeMaster entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await dbContext.ConstructionTypeMasters.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetCanonicalDefaultsAsync(
        Guid tenantId,
        string? resolutionNo,
        DateOnly? resolutionDate,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        List<FormulaSetting> existingSettings = await dbContext.FormulaSettings
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingSettings.Count == 0)
        {
            await defaultFormulaSeeder.SeedDefaultsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return;
        }

        const string defaultPeriodName = "2026-27 ते 2029-30";
        FormulaSetting? canonicalPeriod = existingSettings.FirstOrDefault(s => s.RatePeriodName == defaultPeriodName)
            ?? existingSettings.FirstOrDefault(s => s.IsActive)
            ?? existingSettings[0];

        foreach (FormulaSetting setting in existingSettings)
        {
            setting.IsActive = setting.Id == canonicalPeriod.Id;
        }

        canonicalPeriod.RatePeriodName = defaultPeriodName;
        canonicalPeriod.RateStartYear = 2026;
        canonicalPeriod.RateEndYear = 2029;
        canonicalPeriod.TaxDivisor = 1000m;
        canonicalPeriod.NoticeGraceDays = 15;
        canonicalPeriod.EarlyRebatePct = 10.0m;
        canonicalPeriod.LatePenaltyPct = 10.0m;
        canonicalPeriod.ApplyLightingHealthOnOpenPlot = true;
        canonicalPeriod.RoundArea = true;
        canonicalPeriod.IsActive = true;
        canonicalPeriod.LastResolutionNo = resolutionNo;
        canonicalPeriod.LastResolutionDate = resolutionDate;
        canonicalPeriod.LastChangeReason = changeReason;

        Dictionary<string, ConstructionTypeMaster> existingByCode = await dbContext.ConstructionTypeMasters
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ConstructionTypeMaster> canonicalTypes = DefaultFormulaSeeder.CreateConstructionTypes(tenantId);
        foreach (ConstructionTypeMaster canonical in canonicalTypes)
        {
            if (existingByCode.TryGetValue(canonical.Code, out ConstructionTypeMaster? existing))
            {
                existing.TypeName = canonical.TypeName;
                existing.Category = canonical.Category;
                existing.LandAnnualValueRate = canonical.LandAnnualValueRate;
                existing.ConstructionReadyReckonerRate = canonical.ConstructionReadyReckonerRate;
                existing.UsageWeightageFactor = canonical.UsageWeightageFactor;
                existing.TaxRate = canonical.TaxRate;
                existing.IsActive = true;
                existing.LastResolutionNo = resolutionNo;
                existing.LastResolutionDate = resolutionDate;
                existing.LastChangeReason = changeReason;
            }
            else
            {
                canonical.LastResolutionNo = resolutionNo;
                canonical.LastResolutionDate = resolutionDate;
                canonical.LastChangeReason = changeReason;
                await dbContext.ConstructionTypeMasters.AddAsync(canonical, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

