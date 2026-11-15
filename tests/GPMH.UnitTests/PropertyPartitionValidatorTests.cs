using GPMH.Domain.Services;
using Xunit;

namespace GPMH.UnitTests;

public sealed class PropertyPartitionValidatorTests
{
    [Fact]
    public void Validate_PassesWithinPointOnePercentToleranceAndZeroDues()
    {
        PartitionValidationResult result = PropertyPartitionValidator.Validate(
            parentPendingDues: 0m,
            parentAreaSqm: 100.00m,
            childPartitions:
            [
                new ChildPartitionArea("P/1", 60.05m),
                new ChildPartitionArea("P/2", 40.00m),
            ]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_FailsWhenPendingDuesAreNonZero()
    {
        PartitionValidationResult result = PropertyPartitionValidator.Validate(
            parentPendingDues: 125.50m,
            parentAreaSqm: 100.00m,
            childPartitions:
            [
                new ChildPartitionArea("P/1", 50m),
                new ChildPartitionArea("P/2", 50m),
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("ZERO_DUES_GATE_FAILED", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_FailsWhenChildAreasExceedPointOnePercentTolerance()
    {
        PartitionValidationResult result = PropertyPartitionValidator.Validate(
            parentPendingDues: 0m,
            parentAreaSqm: 100.00m,
            childPartitions:
            [
                new ChildPartitionArea("P/1", 50.15m),
                new ChildPartitionArea("P/2", 50.00m),
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("AREA_CONSERVATION_VIOLATED", StringComparison.Ordinal));
    }
}

