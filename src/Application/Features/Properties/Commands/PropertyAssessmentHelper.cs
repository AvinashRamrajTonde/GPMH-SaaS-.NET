using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Tax.Queries;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Properties.Commands;

public sealed record PropertyOwnerRequestItem(
    Guid CitizenId,
    decimal OwnershipSharePct,
    bool IsPrimaryOwner);

public sealed record PropertyFloorRequestItem(
    int FloorNo,
    string FloorLabel,
    string ConstructionTypeCode,
    string UsageCode,
    decimal LengthFt,
    decimal WidthFt,
    decimal? AreaSqFt,
    int ConstructionStartYear);

internal static class PropertyAssessmentHelper
{
    public static async Task<(
        IReadOnlyList<PropertyOwner>? Owners,
        IReadOnlyList<PropertyConstruction>? Constructions,
        PropertyTaxCalc? TaxCalc,
        PropertyDemand? DraftDemand,
        decimal ComputedBuiltUpSqFt,
        string? PrimaryOwnerNameMr,
        string? PrimaryOwnerNameEn)> PrepareAssessmentAsync(
        Guid tenantId,
        Property property,
        IReadOnlyList<PropertyOwnerRequestItem>? ownerItems,
        IReadOnlyList<PropertyFloorRequestItem>? floorItems,
        bool calculateTax,
        string? openPlotConstructionCode,
        IPropertyRepository propertyRepository,
        ICitizenRepository citizenRepository,
        ISender sender,
        CancellationToken cancellationToken)
    {
        List<PropertyOwner>? owners = null;
        string? primaryOwnerNameMr = null;
        string? primaryOwnerNameEn = null;
        if (ownerItems is not null)
        {
            if (ownerItems.Count == 0)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Owners"] = ["At least one owner must be assigned when ownership details are submitted."],
                });
            }

            decimal shareTotal = ownerItems.Sum(o => o.OwnershipSharePct);
            if (Math.Abs(shareTotal - 100.00m) > 0.01m)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Owners"] = [$"Joint ownership percentages must equal 100.00% (current total: {shareTotal:F2}%)."],
                });
            }

            if (!ownerItems.Any(o => o.IsPrimaryOwner))
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Owners"] = ["Exactly one primary owner must be designated."],
                });
            }

            HashSet<Guid> distinctCitizenIds = [.. ownerItems.Select(o => o.CitizenId)];
            if (distinctCitizenIds.Count != ownerItems.Count)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Owners"] = ["Duplicate citizen entries are not allowed in property ownership."],
                });
            }

            IReadOnlyDictionary<Guid, Citizen> existingCitizens = await citizenRepository
                .GetByIdsAsync(distinctCitizenIds, cancellationToken)
                .ConfigureAwait(false);

            List<Guid> missingIds = [.. distinctCitizenIds.Where(id => !existingCitizens.ContainsKey(id))];
            if (missingIds.Count > 0)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Owners"] = ["One or more selected citizens were not found in this Gram Panchayat."],
                });
            }

            owners = [.. ownerItems.Select(o => new PropertyOwner
            {
                TenantId = tenantId,
                PropertyId = property.Id,
                CitizenId = o.CitizenId,
                OwnershipSharePct = Math.Round(o.OwnershipSharePct, 2, MidpointRounding.AwayFromZero),
                IsPrimaryOwner = o.IsPrimaryOwner,
                IsActive = true,
            })];

            PropertyOwnerRequestItem primaryItem = ownerItems.First(o => o.IsPrimaryOwner);
            Citizen primaryCitizen = existingCitizens[primaryItem.CitizenId];
            primaryOwnerNameMr = primaryCitizen.FullNameMr;
            primaryOwnerNameEn = primaryCitizen.FullNameEn;
        }

        List<PropertyConstruction>? constructions = null;
        decimal builtUpSqFt = property.BuiltUpAreaSqFt;
        List<MasterFloorTaxRequest> masterFloors = [];

        if (floorItems is not null)
        {
            constructions = [];
            builtUpSqFt = 0m;

            for (int i = 0; i < floorItems.Count; i++)
            {
                PropertyFloorRequestItem f = floorItems[i];
                decimal areaSqFt = f.LengthFt > 0m && f.WidthFt > 0m
                    ? f.LengthFt * f.WidthFt
                    : (f.AreaSqFt ?? 0m);

                if (areaSqFt <= 0m)
                {
                    throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [$"Floors[{i}]"] = ["Each floor must have positive dimensions (Length x Width) or built-up area."],
                    });
                }

                decimal areaSqM = Math.Round(
                    areaSqFt * CapitalValueTaxCalculator.SqFtToSqM,
                    2,
                    MidpointRounding.AwayFromZero);

                builtUpSqFt += areaSqFt;
                string code = f.ConstructionTypeCode.Trim().ToUpperInvariant();
                string usage = f.UsageCode.Trim().ToUpperInvariant();

                constructions.Add(new PropertyConstruction
                {
                    TenantId = tenantId,
                    PropertyId = property.Id,
                    FloorNo = f.FloorNo,
                    FloorLabel = string.IsNullOrWhiteSpace(f.FloorLabel)
                        ? (f.FloorNo == 0 ? "तळमजला" : $"मजला {f.FloorNo}")
                        : f.FloorLabel.Trim(),
                    ConstructionTypeCode = code,
                    UsageCode = usage,
                    LengthFt = f.LengthFt,
                    WidthFt = f.WidthFt,
                    AreaSqFt = Math.Round(areaSqFt, 4, MidpointRounding.AwayFromZero),
                    AreaSqM = areaSqM,
                    ConstructionStartYear = f.ConstructionStartYear,
                    IsActive = true,
                });

                masterFloors.Add(new MasterFloorTaxRequest(
                    ConstructionTypeCode: code,
                    UsageCode: usage,
                    ConstructionStartYear: f.ConstructionStartYear,
                    LengthFt: f.LengthFt,
                    WidthFt: f.WidthFt,
                    AreaSqFt: f.LengthFt > 0m && f.WidthFt > 0m ? null : areaSqFt));
            }
        }

        PropertyTaxCalc? taxCalc = null;
        PropertyDemand? draftDemand = null;

        bool shouldCalculate = calculateTax || (floorItems is { Count: > 0 });
        if (shouldCalculate && (masterFloors.Count > 0 || property.OpenPlotAreaSqFt > 0m))
        {
            AssessmentYear activeYear = await propertyRepository
                .GetActiveAssessmentYearAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["AssessmentYear"] = ["An active financial assessment year is required before assessing property tax."],
                });

            CalculatedPropertyTaxDto calcResult = await sender.Send(
                new CalculatePropertyTaxFromMastersQuery(
                    AssessmentStartYear: activeYear.StartYear,
                    Floors: masterFloors,
                    OpenAreaSqFt: property.OpenPlotAreaSqFt,
                    OpenPlotConstructionCode: string.IsNullOrWhiteSpace(openPlotConstructionCode)
                        ? "OPEN_PLOT"
                        : openPlotConstructionCode.Trim(),
                    WaterConnectionCode: property.WaterConnectionCode,
                    OtherTax: property.OtherTax),
                cancellationToken).ConfigureAwait(false);

            if (constructions is not null)
            {
                for (int i = 0; i < Math.Min(constructions.Count, calcResult.Calculation.FloorBreakdowns.Count); i++)
                {
                    constructions[i].AreaSqM = calcResult.Calculation.FloorBreakdowns[i].AreaSqm;
                }
            }

            PropertyTaxCalculationResult r = calcResult.Calculation;
            taxCalc = new PropertyTaxCalc
            {
                TenantId = tenantId,
                PropertyId = property.Id,
                AssessmentYearId = activeYear.Id,
                BuildingTax = r.BuildingTax,
                OpenLandTax = r.OpenLandTax,
                LightingTax = r.LightingTax,
                HealthTax = r.HealthTax,
                SanitationTax = r.SanitationTax,
                ToiletTax = r.ToiletTax,
                WaterTax = r.WaterTax,
                OtherTax = r.OtherTax,
                TotalTax = r.TotalTax,
                CalculationSnapshotJson = JsonSerializer.Serialize(calcResult),
            };

            decimal assessedCurrentDemand = property.IsTaxExempt ? 0m : r.TotalTax;
            draftDemand = new PropertyDemand
            {
                TenantId = tenantId,
                PropertyId = property.Id,
                AssessmentYearId = activeYear.Id,
                ArrearsBalance = 0m,
                CurrentDemand = assessedCurrentDemand,
                RebateAmount = 0m,
                PenaltyAmount = 0m,
                PaidAmount = 0m,
                PendingBalance = assessedCurrentDemand,
                Status = "DraftAssessment",
            };
        }

        return (owners, constructions, taxCalc, draftDemand, builtUpSqFt, primaryOwnerNameMr, primaryOwnerNameEn);
    }
}

internal static class PropertyMappingExtensions
{
    public static PropertyDto ToDto(this Property property) =>
        new(
            Id: property.Id,
            TenantId: property.TenantId,
            ParentPropertyId: property.ParentPropertyId,
            WardNo: property.WardNo,
            PropertyNo: property.PropertyNo,
            BuiltUpAreaSqFt: property.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt: property.OpenPlotAreaSqFt,
            TotalAreaSqFt: property.TotalAreaSqFt,
            BoundaryEast: property.BoundaryEast,
            BoundaryWest: property.BoundaryWest,
            BoundaryNorth: property.BoundaryNorth,
            BoundarySouth: property.BoundarySouth,
            WaterConnectionCode: property.WaterConnectionCode,
            IsTaxExempt: property.IsTaxExempt,
            IsActive: property.IsActive,
            StreetOrLaneName: property.StreetOrLaneName,
            CitySurveyOrGutNo: property.CitySurveyOrGutNo,
            OccupierName: property.OccupierName,
            PropertyDescription: property.PropertyDescription,
            OtherTax: property.OtherTax,
            Status: property.Status);
}

