using GPMH.Domain.Services;
using Xunit;

namespace GPMH.UnitTests;

public sealed class CapitalValueTaxCalculatorTests
{
    private static readonly IReadOnlyList<DepreciationSlabInput> CanonicalSlabs =
    [
        new(0, 2, 100m),
        new(2, 5, 95m),
        new(5, 10, 85m),
        new(10, 20, 75m),
        new(20, 30, 60m),
        new(30, 40, 45m),
        new(40, 50, 35m),
        new(50, 60, 25m),
        new(60, 100, 15m),
    ];

    [Fact]
    public void Calculate_ChincholiKhurdGoldenVector_Produces66BuildingAnd246Total()
    {
        PropertyTaxCalculationInput input = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new FloorTaxInput(
                    LengthFt: 15m,
                    WidthFt: 15m,
                    AreaSqm: null,
                    LandAnnualValueRate: 760m,
                    ConstructionReadyReckonerRate: 5852m,
                    UsageWeightage: 1.0m,
                    TaxRate: 0.5m,
                    ConstructionStartYear: 2021),
            ],
            DepreciationSlabs: CanonicalSlabs,
            AreaSlabTaxes: new AreaSlabTaxInput(
                LightingTax: 40m,
                HealthTax: 40m,
                SanitationTax: 100m,
                ToiletTax: 0m),
            WaterTax: 0m);

        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(input);

        Assert.Single(result.FloorBreakdowns);
        Assert.Equal(20.90m, result.FloorBreakdowns[0].AreaSqm);
        Assert.Equal(5, result.FloorBreakdowns[0].BuildingAge);
        Assert.Equal(95m, result.FloorBreakdowns[0].DepreciationPercentage);
        Assert.Equal(66m, result.BuildingTax);
        Assert.Equal(246m, result.TotalTax);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(2, 100)]
    [InlineData(3, 95)]
    [InlineData(5, 95)]
    [InlineData(6, 85)]
    [InlineData(10, 85)]
    [InlineData(20, 75)]
    [InlineData(60, 25)]
    [InlineData(100, 15)]
    [InlineData(120, 15)]
    public void ResolveDepreciationPercentage_EnforcesStrictUpperInclusiveBoundaries(
        int buildingAge,
        decimal expectedPct)
    {
        decimal actual = CapitalValueTaxCalculator.ResolveDepreciationPercentage(buildingAge, CanonicalSlabs);
        Assert.Equal(expectedPct, actual);
    }

    [Fact]
    public void Calculate_UsesAwayFromZeroMidpointRounding()
    {
        // CapitalValue = 100 * 1000 * (100/100) = 100_000; tax = 100_000 * 1.0 * 0.665 / 1000 = 66.5 -> 67
        PropertyTaxCalculationInput input = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new FloorTaxInput(
                    LengthFt: 0m,
                    WidthFt: 0m,
                    AreaSqm: 100m,
                    LandAnnualValueRate: 0m,
                    ConstructionReadyReckonerRate: 1000m,
                    UsageWeightage: 1.0m,
                    TaxRate: 0.665m,
                    ConstructionStartYear: 2025),
            ],
            DepreciationSlabs: CanonicalSlabs,
            AreaSlabTaxes: new AreaSlabTaxInput(0m, 0m, 0m, 0m));

        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(input);
        Assert.Equal(67m, result.BuildingTax);
    }

    [Fact]
    public void Calculate_ComputesOpenLandTaxAccurately()
    {
        PropertyTaxCalculationInput input = new(
            AssessmentStartYear: 2026,
            Floors: [],
            DepreciationSlabs: CanonicalSlabs,
            AreaSlabTaxes: new AreaSlabTaxInput(40m, 40m, 0m, 0m),
            OpenAreaSqm: 50m,
            OpenPlotLandRate: 500m,
            OpenPlotTaxRate: 2.0m);

        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(input);
        Assert.Equal(0m, result.BuildingTax);
        Assert.Equal(50m, result.OpenLandTax);
        Assert.Equal(130m, result.TotalTax);
    }

    [Fact]
    public void Calculate_NegativeBuildingAge_ThrowsArgumentOutOfRangeException()
    {
        PropertyTaxCalculationInput input = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new FloorTaxInput(10m, 10m, null, 100m, 1000m, 1.0m, 0.5m, ConstructionStartYear: 2027),
            ],
            DepreciationSlabs: CanonicalSlabs,
            AreaSlabTaxes: new AreaSlabTaxInput(0m, 0m, 0m, 0m));

        Assert.Throws<ArgumentOutOfRangeException>(() => CapitalValueTaxCalculator.Calculate(input));
    }

    private static readonly IReadOnlyList<DepreciationSlabInput> StatutoryHutMudSlabs =
    [
        new(0, 2, 100m),
        new(2, 5, 95m),
        new(5, 10, 85m),
        new(10, 20, 75m),
        new(20, 30, 60m),
        new(30, 40, 45m),
        new(40, 50, 30m),
        new(50, 60, 20m),
        new(60, 100, 15m),
    ];

    private static readonly IReadOnlyList<DepreciationSlabInput> StatutoryStoneBrickRccSlabs =
    [
        new(0, 2, 100m),
        new(2, 5, 95m),
        new(5, 10, 90m),
        new(10, 20, 80m),
        new(20, 30, 70m),
        new(30, 40, 60m),
        new(40, 50, 50m),
        new(50, 60, 40m),
        new(60, 100, 30m),
    ];

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(2, 100, 100)]
    [InlineData(3, 95, 95)]
    [InlineData(5, 95, 95)]
    [InlineData(6, 85, 90)]
    [InlineData(10, 85, 90)]
    [InlineData(20, 75, 80)]
    [InlineData(30, 60, 70)]
    [InlineData(40, 45, 60)]
    [InlineData(50, 30, 50)]
    [InlineData(60, 20, 40)]
    [InlineData(100, 15, 30)]
    [InlineData(120, 15, 30)]
    public void ResolveDepreciationPercentage_AllStatutoryCutoffsMatchBothCategories(
        int buildingAge,
        decimal expectedHutMudPct,
        decimal expectedStoneBrickRccPct)
    {
        decimal actualHutMud = CapitalValueTaxCalculator.ResolveDepreciationPercentage(buildingAge, StatutoryHutMudSlabs);
        decimal actualStoneRcc = CapitalValueTaxCalculator.ResolveDepreciationPercentage(buildingAge, StatutoryStoneBrickRccSlabs);

        Assert.Equal(expectedHutMudPct, actualHutMud);
        Assert.Equal(expectedStoneBrickRccPct, actualStoneRcc);
    }

    [Fact]
    public void Calculate_MixedCategoryFloors_AppliesDistinctDepreciationCurvesPerFloor()
    {
        // Ground floor RCC (age 15 -> 80% retention): 100 sqm, Land 760, RR 18392, Weight 1.0, Tax 1.5
        // First floor HUT_MUD (age 15 -> 75% retention): 100 sqm, Land 760, RR 5852, Weight 1.0, Tax 0.5
        PropertyTaxCalculationInput input = new(
            AssessmentStartYear: 2026,
            Floors:
            [
                new FloorTaxInput(0m, 0m, 100m, 760m, 18392m, 1.0m, 1.5m, 2011, StatutoryStoneBrickRccSlabs),
                new FloorTaxInput(0m, 0m, 100m, 760m, 5852m, 1.0m, 0.5m, 2011, StatutoryHutMudSlabs),
            ],
            DepreciationSlabs: StatutoryStoneBrickRccSlabs,
            AreaSlabTaxes: new AreaSlabTaxInput(100m, 100m, 100m, 0m),
            WaterTax: 720m);

        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(input);

        Assert.Equal(2, result.FloorBreakdowns.Count);
        Assert.Equal(80m, result.FloorBreakdowns[0].DepreciationPercentage);
        Assert.Equal(75m, result.FloorBreakdowns[1].DepreciationPercentage);
        // Floor 0 raw = (76000 + 1839200 * 0.8) * 1.5 / 1000 = 1547360 * 0.0015 = 2321.04
        // Floor 1 raw = (76000 + 585200 * 0.75) * 0.5 / 1000 = 514900 * 0.0005 = 257.45
        // Sum = 2578.49 -> rounded 2578 + 300 area cess + 720 water = 3598
        Assert.Equal(2321.04m, result.FloorBreakdowns[0].RawBuildingTax);
        Assert.Equal(257.45m, result.FloorBreakdowns[1].RawBuildingTax);
        Assert.Equal(2578m, result.BuildingTax);
        Assert.Equal(3598m, result.TotalTax);
    }
}

