using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class PropertyRepository(AppDbContext dbContext) : IPropertyRepository
{
    public async Task<IReadOnlyList<Property>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Properties
            .AsNoTracking()
            .OrderBy(p => p.WardNo)
            .ThenBy(p => p.PropertyNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PropertyDto>> ListEnrichedAsync(CancellationToken cancellationToken)
    {
        List<Property> properties = await dbContext.Properties
            .AsNoTracking()
            .OrderBy(p => p.WardNo)
            .ThenBy(p => p.PropertyNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (properties.Count == 0)
        {
            return [];
        }

        AssessmentYear? activeYear = await GetActiveAssessmentYearAsync(cancellationToken).ConfigureAwait(false);
        List<Guid> propIds = [.. properties.Select(p => p.Id)];

        var ownerJoinRaw = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.IsActive && propIds.Contains(po.PropertyId)
            select new
            {
                po.PropertyId,
                po.IsPrimaryOwner,
                po.OwnershipSharePct,
                c.FullNameMr,
                c.FullNameEn,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ownerJoin = ownerJoinRaw
            .OrderByDescending(x => x.IsPrimaryOwner)
            .ThenByDescending(x => x.OwnershipSharePct)
            .ToList();

        Dictionary<Guid, (string NameMr, string NameEn)> primaryOwnerByProp = [];
        foreach (var item in ownerJoin)
        {
            primaryOwnerByProp.TryAdd(item.PropertyId, (item.FullNameMr, item.FullNameEn));
        }

        Dictionary<Guid, int> floorCounts = await dbContext.PropertyConstructions
            .AsNoTracking()
            .Where(fc => fc.IsActive && propIds.Contains(fc.PropertyId))
            .GroupBy(fc => fc.PropertyId)
            .Select(g => new { PropertyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PropertyId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> taxTotals = [];
        Dictionary<Guid, decimal> demandBalances = [];

        if (activeYear is not null)
        {
            taxTotals = await dbContext.PropertyTaxCalcs
                .AsNoTracking()
                .Where(tc => tc.AssessmentYearId == activeYear.Id && propIds.Contains(tc.PropertyId))
                .ToDictionaryAsync(tc => tc.PropertyId, tc => tc.TotalTax, cancellationToken)
                .ConfigureAwait(false);

            demandBalances = await dbContext.PropertyDemands
                .AsNoTracking()
                .Where(d => d.AssessmentYearId == activeYear.Id && propIds.Contains(d.PropertyId))
                .ToDictionaryAsync(d => d.PropertyId, d => d.PendingBalance, cancellationToken)
                .ConfigureAwait(false);
        }

        return [.. properties.Select(p =>
        {
            primaryOwnerByProp.TryGetValue(p.Id, out (string NameMr, string NameEn) ownerNames);
            return new PropertyDto(
                Id: p.Id,
                TenantId: p.TenantId,
                ParentPropertyId: p.ParentPropertyId,
                WardNo: p.WardNo,
                PropertyNo: p.PropertyNo,
                BuiltUpAreaSqFt: p.BuiltUpAreaSqFt,
                OpenPlotAreaSqFt: p.OpenPlotAreaSqFt,
                TotalAreaSqFt: p.TotalAreaSqFt,
                BoundaryEast: p.BoundaryEast,
                BoundaryWest: p.BoundaryWest,
                BoundaryNorth: p.BoundaryNorth,
                BoundarySouth: p.BoundarySouth,
                WaterConnectionCode: p.WaterConnectionCode,
                IsTaxExempt: p.IsTaxExempt,
                IsActive: p.IsActive,
                StreetOrLaneName: p.StreetOrLaneName,
                CitySurveyOrGutNo: p.CitySurveyOrGutNo,
                OccupierName: p.OccupierName,
                PropertyDescription: p.PropertyDescription,
                OtherTax: p.OtherTax,
                Status: p.Status,
                PrimaryOwnerNameMr: string.IsNullOrEmpty(ownerNames.NameMr) ? null : ownerNames.NameMr,
                PrimaryOwnerNameEn: string.IsNullOrEmpty(ownerNames.NameEn) ? null : ownerNames.NameEn,
                TotalAnnualTax: taxTotals.TryGetValue(p.Id, out decimal t) ? t : null,
                PendingBalance: demandBalances.TryGetValue(p.Id, out decimal bal) ? bal : null,
                FloorCount: floorCounts.GetValueOrDefault(p.Id));
        })];
    }

    public async Task<PropertyHubKpisDto> GetHubKpisAsync(CancellationToken cancellationToken)
    {
        var props = await dbContext.Properties
            .AsNoTracking()
            .Select(p => new { p.WardNo, p.IsTaxExempt, p.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? activeYear = await GetActiveAssessmentYearAsync(cancellationToken).ConfigureAwait(false);
        decimal totalDemand = 0m;
        if (activeYear is not null)
        {
            List<decimal> demands = await dbContext.PropertyDemands
                .AsNoTracking()
                .Where(d => d.AssessmentYearId == activeYear.Id)
                .Select(d => d.CurrentDemand)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            totalDemand = demands.Sum();
        }

        return new PropertyHubKpisDto(
            TotalProperties: props.Count,
            TotalAssessedDemand: totalDemand,
            ExemptPropertiesCount: props.Count(p => p.IsTaxExempt),
            ActiveWardsCount: props.Select(p => p.WardNo).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PartitionedCount: props.Count(p => string.Equals(p.Status, "Partitioned", StringComparison.OrdinalIgnoreCase)));
    }

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PropertyDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Property? property = await dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return null;
        }

        List<PropertyOwnerDetailDto> ownersRaw = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.PropertyId == id && po.IsActive
            select new PropertyOwnerDetailDto(
                po.Id,
                po.CitizenId,
                c.CitizenCode,
                c.FullNameMr,
                c.FullNameEn,
                c.MobileNo,
                string.IsNullOrWhiteSpace(c.AadhaarLast4) ? null : $"XXXX-XXXX-{c.AadhaarLast4}",
                c.ExemptionCategory,
                po.OwnershipSharePct,
                po.IsPrimaryOwner,
                po.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PropertyOwnerDetailDto> owners =
        [
            .. ownersRaw
                .OrderByDescending(o => o.IsPrimaryOwner)
                .ThenByDescending(o => o.OwnershipSharePct),
        ];

        List<PropertyConstructionDto> constructions = await dbContext.PropertyConstructions
            .AsNoTracking()
            .Where(fc => fc.PropertyId == id && fc.IsActive)
            .OrderBy(fc => fc.FloorNo)
            .Select(fc => new PropertyConstructionDto(
                fc.Id,
                fc.FloorNo,
                fc.FloorLabel,
                fc.ConstructionTypeCode,
                fc.UsageCode,
                fc.LengthFt,
                fc.WidthFt,
                fc.AreaSqFt,
                fc.AreaSqM,
                fc.ConstructionStartYear,
                fc.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        AssessmentYear? activeYear = await GetActiveAssessmentYearAsync(cancellationToken).ConfigureAwait(false);

        PropertyTaxCalcDto? taxCalcDto = null;
        PropertyDemandDto? demandDto = null;

        if (activeYear is not null)
        {
            PropertyTaxCalc? tc = await dbContext.PropertyTaxCalcs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.PropertyId == id && t.AssessmentYearId == activeYear.Id, cancellationToken)
                .ConfigureAwait(false);
            if (tc is not null)
            {
                taxCalcDto = new PropertyTaxCalcDto(
                    tc.Id,
                    tc.AssessmentYearId,
                    activeYear.YearLabel,
                    tc.BuildingTax,
                    tc.OpenLandTax,
                    tc.LightingTax,
                    tc.HealthTax,
                    tc.SanitationTax,
                    tc.ToiletTax,
                    tc.WaterTax,
                    tc.OtherTax,
                    tc.TotalTax,
                    tc.CalculationSnapshotJson,
                    tc.UpdatedAtUtc);
            }

            PropertyDemand? dem = await dbContext.PropertyDemands
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.PropertyId == id && d.AssessmentYearId == activeYear.Id, cancellationToken)
                .ConfigureAwait(false);
            if (dem is not null)
            {
                demandDto = new PropertyDemandDto(
                    dem.Id,
                    dem.AssessmentYearId,
                    activeYear.YearLabel,
                    dem.ArrearsBalance,
                    dem.CurrentDemand,
                    dem.RebateAmount,
                    dem.PenaltyAmount,
                    dem.PaidAmount,
                    dem.PendingBalance,
                    dem.Status);
            }
        }

        List<ChildPropertySummaryDto> children = await dbContext.Properties
            .AsNoTracking()
            .Where(cp => cp.ParentPropertyId == id)
            .OrderBy(cp => cp.PropertyNo)
            .Select(cp => new ChildPropertySummaryDto(cp.Id, cp.WardNo, cp.PropertyNo, cp.TotalAreaSqFt, cp.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PropertyDetailDto(
            Id: property.Id,
            TenantId: property.TenantId,
            ParentPropertyId: property.ParentPropertyId,
            WardNo: property.WardNo,
            PropertyNo: property.PropertyNo,
            StreetOrLaneName: property.StreetOrLaneName,
            CitySurveyOrGutNo: property.CitySurveyOrGutNo,
            OccupierName: property.OccupierName,
            PropertyDescription: property.PropertyDescription,
            BuiltUpAreaSqFt: property.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt: property.OpenPlotAreaSqFt,
            TotalAreaSqFt: property.TotalAreaSqFt,
            BoundaryEast: property.BoundaryEast,
            BoundaryWest: property.BoundaryWest,
            BoundaryNorth: property.BoundaryNorth,
            BoundarySouth: property.BoundarySouth,
            WaterConnectionCode: property.WaterConnectionCode,
            OtherTax: property.OtherTax,
            IsTaxExempt: property.IsTaxExempt,
            Status: property.Status,
            IsActive: property.IsActive,
            Owners: owners,
            Constructions: constructions,
            TaxCalc: taxCalcDto,
            Demand: demandDto,
            ChildProperties: children);
    }

    public Task<bool> ExistsByWardAndNoAsync(string wardNo, string propertyNo, CancellationToken cancellationToken) =>
        dbContext.Properties.AnyAsync(p => p.WardNo == wardNo && p.PropertyNo == propertyNo, cancellationToken);

    public Task<AssessmentYear?> GetActiveAssessmentYearAsync(CancellationToken cancellationToken) =>
        dbContext.AssessmentYears
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.StartYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        await dbContext.Properties.AddAsync(property, cancellationToken).ConfigureAwait(false);
    }

    public async Task PersistFullAssessmentAsync(
        Property property,
        IReadOnlyList<PropertyOwner>? owners,
        IReadOnlyList<PropertyConstruction>? constructions,
        PropertyTaxCalc? taxCalc,
        PropertyDemand? draftDemand,
        bool replaceOwnersAndConstructions,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            if (replaceOwnersAndConstructions && owners is not null)
            {
                List<PropertyOwner> oldOwners = await dbContext.PropertyOwners
                    .Where(po => po.PropertyId == property.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (oldOwners.Count > 0)
                {
                    dbContext.PropertyOwners.RemoveRange(oldOwners);
                }
            }

            if (owners is { Count: > 0 })
            {
                await dbContext.PropertyOwners.AddRangeAsync(owners, cancellationToken).ConfigureAwait(false);
            }

            if (replaceOwnersAndConstructions && constructions is not null)
            {
                List<PropertyConstruction> oldConstructions = await dbContext.PropertyConstructions
                    .Where(fc => fc.PropertyId == property.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (oldConstructions.Count > 0)
                {
                    dbContext.PropertyConstructions.RemoveRange(oldConstructions);
                }
            }

            if (constructions is { Count: > 0 })
            {
                await dbContext.PropertyConstructions.AddRangeAsync(constructions, cancellationToken).ConfigureAwait(false);
            }

            if (taxCalc is not null)
            {
                PropertyTaxCalc? existingCalc = await dbContext.PropertyTaxCalcs
                    .FirstOrDefaultAsync(
                        t => t.PropertyId == property.Id && t.AssessmentYearId == taxCalc.AssessmentYearId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existingCalc is not null)
                {
                    existingCalc.BuildingTax = taxCalc.BuildingTax;
                    existingCalc.OpenLandTax = taxCalc.OpenLandTax;
                    existingCalc.LightingTax = taxCalc.LightingTax;
                    existingCalc.HealthTax = taxCalc.HealthTax;
                    existingCalc.SanitationTax = taxCalc.SanitationTax;
                    existingCalc.ToiletTax = taxCalc.ToiletTax;
                    existingCalc.WaterTax = taxCalc.WaterTax;
                    existingCalc.OtherTax = taxCalc.OtherTax;
                    existingCalc.TotalTax = taxCalc.TotalTax;
                    existingCalc.CalculationSnapshotJson = taxCalc.CalculationSnapshotJson;
                }
                else
                {
                    await dbContext.PropertyTaxCalcs.AddAsync(taxCalc, cancellationToken).ConfigureAwait(false);
                }
            }

            if (draftDemand is not null)
            {
                PropertyDemand? existingDemand = await dbContext.PropertyDemands
                    .FirstOrDefaultAsync(
                        d => d.PropertyId == property.Id && d.AssessmentYearId == draftDemand.AssessmentYearId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existingDemand is not null)
                {
                    existingDemand.CurrentDemand = draftDemand.CurrentDemand;
                    existingDemand.PendingBalance = Math.Max(
                        0m,
                        existingDemand.ArrearsBalance
                        + draftDemand.CurrentDemand
                        + existingDemand.PenaltyAmount
                        - existingDemand.RebateAmount
                        - existingDemand.PaidAmount);
                }
                else
                {
                    await dbContext.PropertyDemands.AddAsync(draftDemand, cancellationToken).ConfigureAwait(false);
                }
            }

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

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
