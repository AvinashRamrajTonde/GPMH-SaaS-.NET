namespace GPMH.Domain.Services;

public sealed record DepreciationSlabInput(int AgeFrom, int AgeTo, decimal RetentionPercentage);

public sealed record FloorTaxInput(
    decimal LengthFt,
    decimal WidthFt,
    decimal? AreaSqm,
    decimal LandAnnualValueRate,
    decimal ConstructionReadyReckonerRate,
    decimal UsageWeightage,
    decimal TaxRate,
    int ConstructionStartYear);

public sealed record AreaSlabTaxInput(
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax);

public sealed record PropertyTaxCalculationInput(
    int AssessmentStartYear,
    IReadOnlyList<FloorTaxInput> Floors,
    IReadOnlyList<DepreciationSlabInput> DepreciationSlabs,
    AreaSlabTaxInput AreaSlabTaxes,
    decimal WaterTax = 0m,
    decimal OtherTax = 0m,
    decimal OpenAreaSqm = 0m,
    decimal OpenPlotLandRate = 0m,
    decimal OpenPlotTaxRate = 0m,
    decimal TaxDivisor = CapitalValueTaxCalculator.DefaultTaxDivisor,
    bool RoundAreaToTwoDecimals = true);

public sealed record FloorTaxBreakdown(
    int FloorIndex,
    decimal AreaSqm,
    int BuildingAge,
    decimal DepreciationPercentage,
    decimal LandValue,
    decimal DepreciatedBuildingValue,
    decimal FloorCapitalValue,
    decimal RawBuildingTax);

public sealed record PropertyTaxCalculationResult(
    IReadOnlyList<FloorTaxBreakdown> FloorBreakdowns,
    decimal BuildingTax,
    decimal OpenLandTax,
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax,
    decimal WaterTax,
    decimal OtherTax,
    decimal TotalTax);

/// <summary>
/// Canonical Maharashtra Gram Panchayat Capital Value Property Tax Engine (Namuna 8).
/// Formula locked per ADR-016 and docs/statutory/canonical-tax-specification.md.
/// </summary>
public static class CapitalValueTaxCalculator
{
    public const decimal SqFtToSqM = 0.09290304m;
    public const decimal DefaultTaxDivisor = 1000m;

    public static PropertyTaxCalculationResult Calculate(PropertyTaxCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TaxDivisor);
        if (input.DepreciationSlabs.Count == 0)
        {
            throw new ArgumentException("At least one depreciation slab must be supplied.", nameof(input));
        }

        IReadOnlyList<DepreciationSlabInput> orderedSlabs = input.DepreciationSlabs
            .OrderBy(s => s.AgeFrom)
            .ThenBy(s => s.AgeTo)
            .ToArray();

        List<FloorTaxBreakdown> breakdowns = new(input.Floors.Count);
        decimal totalRawBuildingTax = 0m;

        for (int idx = 0; idx < input.Floors.Count; idx++)
        {
            FloorTaxInput floor = input.Floors[idx];
            ArgumentOutOfRangeException.ThrowIfNegative(floor.LengthFt);
            ArgumentOutOfRangeException.ThrowIfNegative(floor.WidthFt);
            ArgumentOutOfRangeException.ThrowIfNegative(floor.LandAnnualValueRate);
            ArgumentOutOfRangeException.ThrowIfNegative(floor.ConstructionReadyReckonerRate);
            ArgumentOutOfRangeException.ThrowIfNegative(floor.UsageWeightage);
            ArgumentOutOfRangeException.ThrowIfNegative(floor.TaxRate);

            decimal areaSqm = floor.AreaSqm.HasValue
                ? floor.AreaSqm.Value
                : floor.LengthFt * floor.WidthFt * SqFtToSqM;

            if (!floor.AreaSqm.HasValue && input.RoundAreaToTwoDecimals)
            {
                areaSqm = Math.Round(areaSqm, 2, MidpointRounding.AwayFromZero);
            }

            ArgumentOutOfRangeException.ThrowIfNegative(areaSqm);

            int buildingAge = input.AssessmentStartYear - floor.ConstructionStartYear;
            if (buildingAge < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    $"Construction start year ({floor.ConstructionStartYear}) cannot exceed assessment start year ({input.AssessmentStartYear}).");
            }

            decimal retentionPct = ResolveDepreciationPercentage(buildingAge, orderedSlabs);
            decimal landValue = areaSqm * floor.LandAnnualValueRate;
            decimal depreciatedBuildingValue = areaSqm * floor.ConstructionReadyReckonerRate * (retentionPct / 100m);
            decimal capitalValue = landValue + depreciatedBuildingValue;
            decimal rawBuildingTax = (capitalValue * floor.UsageWeightage * floor.TaxRate) / input.TaxDivisor;

            totalRawBuildingTax += rawBuildingTax;
            breakdowns.Add(new FloorTaxBreakdown(
                FloorIndex: idx,
                AreaSqm: areaSqm,
                BuildingAge: buildingAge,
                DepreciationPercentage: retentionPct,
                LandValue: landValue,
                DepreciatedBuildingValue: depreciatedBuildingValue,
                FloorCapitalValue: capitalValue,
                RawBuildingTax: rawBuildingTax));
        }

        decimal buildingTax = Math.Round(totalRawBuildingTax, 0, MidpointRounding.AwayFromZero);

        ArgumentOutOfRangeException.ThrowIfNegative(input.OpenAreaSqm);
        ArgumentOutOfRangeException.ThrowIfNegative(input.OpenPlotLandRate);
        ArgumentOutOfRangeException.ThrowIfNegative(input.OpenPlotTaxRate);

        decimal openLandTax = input.OpenAreaSqm > 0m
            ? Math.Round(
                (input.OpenAreaSqm * input.OpenPlotLandRate * input.OpenPlotTaxRate) / input.TaxDivisor,
                0,
                MidpointRounding.AwayFromZero)
            : 0m;

        decimal totalTax = buildingTax
            + openLandTax
            + input.AreaSlabTaxes.LightingTax
            + input.AreaSlabTaxes.HealthTax
            + input.AreaSlabTaxes.SanitationTax
            + input.AreaSlabTaxes.ToiletTax
            + input.WaterTax
            + input.OtherTax;

        return new PropertyTaxCalculationResult(
            FloorBreakdowns: breakdowns,
            BuildingTax: buildingTax,
            OpenLandTax: openLandTax,
            LightingTax: input.AreaSlabTaxes.LightingTax,
            HealthTax: input.AreaSlabTaxes.HealthTax,
            SanitationTax: input.AreaSlabTaxes.SanitationTax,
            ToiletTax: input.AreaSlabTaxes.ToiletTax,
            WaterTax: input.WaterTax,
            OtherTax: input.OtherTax,
            TotalTax: totalTax);
    }

    public static decimal ResolveDepreciationPercentage(
        int buildingAge,
        IReadOnlyList<DepreciationSlabInput> orderedSlabs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(buildingAge);
        ArgumentNullException.ThrowIfNull(orderedSlabs);
        if (orderedSlabs.Count == 0)
        {
            throw new ArgumentException("At least one depreciation slab is required.", nameof(orderedSlabs));
        }

        foreach (DepreciationSlabInput slab in orderedSlabs)
        {
            // Slab 1 (AgeFrom == 0) captures Age 0..AgeTo inclusive;
            // subsequent slabs match strictly Age > AgeFrom && Age <= AgeTo.
            bool matches = slab.AgeFrom == 0
                ? buildingAge >= 0 && buildingAge <= slab.AgeTo
                : buildingAge > slab.AgeFrom && buildingAge <= slab.AgeTo;

            if (matches)
            {
                return slab.RetentionPercentage;
            }
        }

        // Structures older than the highest configured AgeTo fall into the final residual slab.
        return orderedSlabs[^1].RetentionPercentage;
    }
}

