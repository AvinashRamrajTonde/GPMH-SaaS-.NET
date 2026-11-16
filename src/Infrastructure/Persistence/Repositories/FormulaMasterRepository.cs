using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Domain.Entities.Taxation;
using GPMH.Infrastructure.Taxation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public async Task<IReadOnlyList<UsageWeightage>> ListUsageWeightagesAsync(CancellationToken cancellationToken)
    {
        List<UsageWeightage> items = await dbContext.UsageWeightages
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. items.OrderBy(u => u.Weightage).ThenBy(u => u.Code)];
    }

    public Task<UsageWeightage?> GetUsageWeightageByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UsageWeightages.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<UsageWeightage?> GetUsageWeightageByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.UsageWeightages.FirstOrDefaultAsync(u => u.Code == code, cancellationToken);

    public async Task<IReadOnlyList<DepreciationSlab>> ListDepreciationSlabsAsync(CancellationToken cancellationToken) =>
        await dbContext.DepreciationSlabs
            .OrderBy(d => d.AgeFrom)
            .ThenBy(d => d.AgeTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceDepreciationSlabsAsync(
        Guid tenantId,
        IReadOnlyList<DepreciationSlab> slabs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slabs);

        List<DepreciationSlab> existing = await dbContext.DepreciationSlabs
            .OrderBy(d => d.AgeFrom)
            .ThenBy(d => d.AgeTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool exactRangeMatch = existing.Count == slabs.Count &&
            Enumerable.Range(0, slabs.Count).All(i =>
                existing[i].AgeFrom == slabs[i].AgeFrom && existing[i].AgeTo == slabs[i].AgeTo);

        if (exactRangeMatch)
        {
            for (int i = 0; i < slabs.Count; i++)
            {
                existing[i].FromInclusive = slabs[i].FromInclusive;
                existing[i].ToInclusive = slabs[i].ToInclusive;
                existing[i].HutMudPercent = slabs[i].HutMudPercent;
                existing[i].StoneBrickRccPercent = slabs[i].StoneBrickRccPercent;
                existing[i].LastResolutionNo = slabs[i].LastResolutionNo;
                existing[i].LastResolutionDate = slabs[i].LastResolutionDate;
                existing[i].LastChangeReason = slabs[i].LastChangeReason;
            }
            return;
        }

        // Flush deletions inside an explicit transaction before inserting modified boundaries so (TenantId, AgeFrom, AgeTo) never collides mid-batch.
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            if (existing.Count > 0)
            {
                dbContext.DepreciationSlabs.RemoveRange(existing);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (DepreciationSlab slab in slabs)
            {
                slab.TenantId = tenantId;
            }
            await dbContext.DepreciationSlabs.AddRangeAsync(slabs, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<AreaSlabTax>> ListAreaSlabTaxesAsync(CancellationToken cancellationToken) =>
        await dbContext.AreaSlabTaxes
            .OrderBy(a => a.AreaFrom)
            .ThenBy(a => a.AreaTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceAreaSlabTaxesAsync(
        Guid tenantId,
        IReadOnlyList<AreaSlabTax> slabs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slabs);

        List<AreaSlabTax> existing = await dbContext.AreaSlabTaxes
            .OrderBy(a => a.AreaFrom)
            .ThenBy(a => a.AreaTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool exactRangeMatch = existing.Count == slabs.Count &&
            Enumerable.Range(0, slabs.Count).All(i =>
                existing[i].AreaFrom == slabs[i].AreaFrom && existing[i].AreaTo == slabs[i].AreaTo);

        if (exactRangeMatch)
        {
            for (int i = 0; i < slabs.Count; i++)
            {
                existing[i].LightingTax = slabs[i].LightingTax;
                existing[i].HealthTax = slabs[i].HealthTax;
                existing[i].SanitationTax = slabs[i].SanitationTax;
                existing[i].ToiletTax = slabs[i].ToiletTax;
                existing[i].LastResolutionNo = slabs[i].LastResolutionNo;
                existing[i].LastResolutionDate = slabs[i].LastResolutionDate;
                existing[i].LastChangeReason = slabs[i].LastChangeReason;
            }
            return;
        }

        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            if (existing.Count > 0)
            {
                dbContext.AreaSlabTaxes.RemoveRange(existing);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (AreaSlabTax slab in slabs)
            {
                slab.TenantId = tenantId;
            }
            await dbContext.AreaSlabTaxes.AddRangeAsync(slabs, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<WaterTaxMaster>> ListWaterTaxesAsync(CancellationToken cancellationToken)
    {
        List<WaterTaxMaster> items = await dbContext.WaterTaxMasters
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. items.OrderBy(w => w.Amount).ThenBy(w => w.Code)];
    }

    public Task<WaterTaxMaster?> GetWaterTaxByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.WaterTaxMasters.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<WaterTaxMaster?> GetWaterTaxByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.WaterTaxMasters.FirstOrDefaultAsync(w => w.Code == code, cancellationToken);

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

    public async Task AddUsageWeightageAsync(UsageWeightage entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await dbContext.UsageWeightages.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddWaterTaxMasterAsync(WaterTaxMaster entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await dbContext.WaterTaxMasters.AddAsync(entity, cancellationToken).ConfigureAwait(false);
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

        await ResetConstructionTypesAsync(tenantId, resolutionNo, resolutionDate, changeReason, cancellationToken).ConfigureAwait(false);
        await ResetUsageWeightagesAsync(tenantId, resolutionNo, resolutionDate, changeReason, cancellationToken).ConfigureAwait(false);
        await ResetWaterTaxesAsync(tenantId, resolutionNo, resolutionDate, changeReason, cancellationToken).ConfigureAwait(false);

        List<DepreciationSlab> canonicalSlabs = [.. DefaultFormulaSeeder.CreateDepreciationSlabs(tenantId)];
        foreach (DepreciationSlab slab in canonicalSlabs)
        {
            slab.LastResolutionNo = resolutionNo;
            slab.LastResolutionDate = resolutionDate;
            slab.LastChangeReason = changeReason;
        }
        await ReplaceDepreciationSlabsAsync(tenantId, canonicalSlabs, cancellationToken).ConfigureAwait(false);

        List<AreaSlabTax> canonicalAreaSlabs = [.. DefaultFormulaSeeder.CreateAreaSlabTaxes(tenantId)];
        foreach (AreaSlabTax slab in canonicalAreaSlabs)
        {
            slab.LastResolutionNo = resolutionNo;
            slab.LastResolutionDate = resolutionDate;
            slab.LastChangeReason = changeReason;
        }
        await ReplaceAreaSlabTaxesAsync(tenantId, canonicalAreaSlabs, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResetConstructionTypesAsync(
        Guid tenantId,
        string? resolutionNo,
        DateOnly? resolutionDate,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        Dictionary<string, ConstructionTypeMaster> existingByCode = await dbContext.ConstructionTypeMasters
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (ConstructionTypeMaster canonical in DefaultFormulaSeeder.CreateConstructionTypes(tenantId))
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

    private async Task ResetUsageWeightagesAsync(
        Guid tenantId,
        string? resolutionNo,
        DateOnly? resolutionDate,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        Dictionary<string, UsageWeightage> existingByCode = await dbContext.UsageWeightages
            .ToDictionaryAsync(u => u.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (UsageWeightage canonical in DefaultFormulaSeeder.CreateUsageWeightages(tenantId))
        {
            if (existingByCode.TryGetValue(canonical.Code, out UsageWeightage? existing))
            {
                existing.UsageName = canonical.UsageName;
                existing.Weightage = canonical.Weightage;
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
                await dbContext.UsageWeightages.AddAsync(canonical, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ResetWaterTaxesAsync(
        Guid tenantId,
        string? resolutionNo,
        DateOnly? resolutionDate,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        Dictionary<string, WaterTaxMaster> existingByCode = await dbContext.WaterTaxMasters
            .ToDictionaryAsync(w => w.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (WaterTaxMaster canonical in DefaultFormulaSeeder.CreateWaterTaxes(tenantId))
        {
            if (existingByCode.TryGetValue(canonical.Code, out WaterTaxMaster? existing))
            {
                existing.WaterTypeName = canonical.WaterTypeName;
                existing.Amount = canonical.Amount;
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
                await dbContext.WaterTaxMasters.AddAsync(canonical, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
