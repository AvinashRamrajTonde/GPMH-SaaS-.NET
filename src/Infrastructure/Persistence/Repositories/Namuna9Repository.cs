using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>Tenant-scoped EF Core repository for Namuna 9 demand registers, adjustments, and FY rollover.</summary>
public sealed class Namuna9Repository(AppDbContext dbContext) : INamuna9Repository
{
    public async Task<IReadOnlyList<AssessmentYear>> ListYearsAsync(CancellationToken cancellationToken)
    {
        List<AssessmentYear> years = await dbContext.AssessmentYears
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. years.OrderByDescending(y => y.StartYear).ThenBy(y => y.YearLabel, StringComparer.Ordinal)];
    }

    public async Task<AssessmentYear?> GetYearAsync(Guid? yearId, CancellationToken cancellationToken)
    {
        if (yearId.HasValue)
        {
            return await dbContext.AssessmentYears
                .FirstOrDefaultAsync(y => y.Id == yearId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        List<AssessmentYear> activeYears = await dbContext.AssessmentYears
            .Where(y => y.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return activeYears
            .OrderByDescending(y => y.StartYear)
            .FirstOrDefault();
    }

    public Task<AssessmentYear?> FindYearByLabelAsync(string yearLabel, CancellationToken cancellationToken)
    {
        string trimmed = yearLabel.Trim();
        return dbContext.AssessmentYears
            .FirstOrDefaultAsync(y => y.YearLabel == trimmed, cancellationToken);
    }

    public async Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken)
    {
        List<FormulaSetting> active = await dbContext.FormulaSettings
            .AsNoTracking()
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return active
            .OrderByDescending(f => f.RateStartYear)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<Namuna9DemandContext>> LoadContextsAsync(
        Guid assessmentYearId,
        string? wardNo,
        string? search,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken)
    {
        IQueryable<PropertyDemand> demandQuery = dbContext.PropertyDemands
            .Where(d => d.AssessmentYearId == assessmentYearId);

        if (propertyIds is { Count: > 0 })
        {
            demandQuery = demandQuery.Where(d => propertyIds.Contains(d.PropertyId));
        }

        List<PropertyDemand> demands = await demandQuery
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (demands.Count == 0)
        {
            return [];
        }

        List<Guid> demandPropIds = [.. demands.Select(d => d.PropertyId).Distinct()];

        List<Property> props = await dbContext.Properties
            .AsNoTracking()
            .Where(p => demandPropIds.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Property> propById = props.ToDictionary(p => p.Id);

        var ownerRows = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.IsActive && demandPropIds.Contains(po.PropertyId)
            select new
            {
                po.PropertyId,
                po.IsPrimaryOwner,
                c.FullNameMr,
                c.FullNameEn,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, (string NameMr, string NameEn)> ownerByProp = [];
        foreach (var row in ownerRows.OrderByDescending(o => o.IsPrimaryOwner))
        {
            ownerByProp.TryAdd(row.PropertyId, (row.FullNameMr, row.FullNameEn));
        }

        List<PropertyTaxCalc> calcs = await dbContext.PropertyTaxCalcs
            .AsNoTracking()
            .Where(tc => tc.AssessmentYearId == assessmentYearId && demandPropIds.Contains(tc.PropertyId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyTaxCalc> calcByProp = calcs.ToDictionary(c => c.PropertyId);

        string? wardFilter = string.IsNullOrWhiteSpace(wardNo) ? null : wardNo.Trim();
        string? searchFilter = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        List<Namuna9DemandContext> results = [];

        foreach (PropertyDemand demand in demands)
        {
            if (!propById.TryGetValue(demand.PropertyId, out Property? prop))
            {
                continue;
            }

            if (wardFilter is not null &&
                !string.Equals(prop.WardNo.Trim(), wardFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ownerByProp.TryGetValue(prop.Id, out (string NameMr, string NameEn) owner);

            if (searchFilter is not null)
            {
                bool matchesProp = prop.PropertyNo.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)
                    || prop.WardNo.Contains(searchFilter, StringComparison.OrdinalIgnoreCase);
                bool matchesOwner = (!string.IsNullOrEmpty(owner.NameMr) && owner.NameMr.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(owner.NameEn) && owner.NameEn.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(prop.OccupierName) && prop.OccupierName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

                if (!matchesProp && !matchesOwner)
                {
                    continue;
                }
            }

            calcByProp.TryGetValue(prop.Id, out PropertyTaxCalc? calc);
            results.Add(new Namuna9DemandContext(
                Demand: demand,
                Property: prop,
                OwnerNameMr: string.IsNullOrEmpty(owner.NameMr) ? prop.OccupierName : owner.NameMr,
                OwnerNameEn: string.IsNullOrEmpty(owner.NameEn) ? prop.OccupierName : owner.NameEn,
                TaxCalc: calc));
        }

        return
        [
            .. results
                .OrderBy(r => r.Property.WardNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Property.PropertyNo, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<Namuna9RolloverInputs> LoadRolloverInputsAsync(
        Guid sourceYearId,
        Guid? targetYearId,
        CancellationToken cancellationToken)
    {
        List<Property> props = await dbContext.Properties
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PropertyDemand> sourceDemandsList = await dbContext.PropertyDemands
            .AsNoTracking()
            .Where(d => d.AssessmentYearId == sourceYearId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PropertyTaxCalc> sourceCalcsList = await dbContext.PropertyTaxCalcs
            .AsNoTracking()
            .Where(c => c.AssessmentYearId == sourceYearId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemand> targetDemands = [];
        Dictionary<Guid, PropertyTaxCalc> targetCalcs = [];

        if (targetYearId.HasValue)
        {
            List<PropertyDemand> targetDemandList = await dbContext.PropertyDemands
                .Where(d => d.AssessmentYearId == targetYearId.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            targetDemands = targetDemandList.ToDictionary(d => d.PropertyId);

            List<PropertyTaxCalc> targetCalcList = await dbContext.PropertyTaxCalcs
                .AsNoTracking()
                .Where(c => c.AssessmentYearId == targetYearId.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            targetCalcs = targetCalcList.ToDictionary(c => c.PropertyId);
        }

        return new Namuna9RolloverInputs(
            Properties: [.. props.OrderBy(p => p.WardNo, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.PropertyNo, StringComparer.OrdinalIgnoreCase)],
            SourceDemands: sourceDemandsList.ToDictionary(d => d.PropertyId),
            SourceCalcs: sourceCalcsList.ToDictionary(c => c.PropertyId),
            TargetDemands: targetDemands,
            TargetCalcs: targetCalcs);
    }

    public Task AddYearAsync(AssessmentYear year, CancellationToken cancellationToken) =>
        dbContext.AssessmentYears.AddAsync(year, cancellationToken).AsTask();

    public Task AddDemandsAsync(IEnumerable<PropertyDemand> demands, CancellationToken cancellationToken) =>
        dbContext.PropertyDemands.AddRangeAsync(demands, cancellationToken);

    public Task AddTaxCalcsAsync(IEnumerable<PropertyTaxCalc> calcs, CancellationToken cancellationToken) =>
        dbContext.PropertyTaxCalcs.AddRangeAsync(calcs, cancellationToken);

    public Task AddAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken) =>
        dbContext.AuditLogs.AddRangeAsync(logs, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            await work(cancellationToken).ConfigureAwait(false);

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
}

