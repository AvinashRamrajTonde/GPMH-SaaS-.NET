namespace GPMH.Domain.Services;

public sealed record ChildPartitionArea(string ChildSuffix, decimal AreaSqm);

public sealed record PartitionValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal ParentAreaSqm,
    decimal TotalChildAreaSqm,
    decimal AreaDeltaSqm,
    decimal AllowedToleranceSqm);

/// <summary>
/// Enforces Namuna 8 ownership split invariants:
/// 1) Zero pending dues (arrears + current balance == 0)
/// 2) Area conservation within +-0.1% across resulting child partitions (P/1, P/2, ...).
/// </summary>
public static class PropertyPartitionValidator
{
    public const decimal AreaToleranceRatio = 0.001m;

    public static PartitionValidationResult Validate(
        decimal parentPendingDues,
        decimal parentAreaSqm,
        IReadOnlyList<ChildPartitionArea> childPartitions)
    {
        ArgumentNullException.ThrowIfNull(childPartitions);

        List<string> errors = [];

        if (parentPendingDues != 0m)
        {
            errors.Add($"ZERO_DUES_GATE_FAILED: Property has pending dues of {parentPendingDues:F2} INR and cannot be partitioned.");
        }

        if (parentAreaSqm <= 0m)
        {
            errors.Add("INVALID_PARENT_AREA: Parent property area must be strictly greater than 0 sq.m.");
        }

        if (childPartitions.Count < 2)
        {
            errors.Add("MINIMUM_TWO_PARTITIONS_REQUIRED: A property split must produce at least two child partitions.");
        }

        decimal totalChildArea = 0m;
        HashSet<string> suffixes = new(StringComparer.OrdinalIgnoreCase);

        foreach (ChildPartitionArea child in childPartitions)
        {
            if (string.IsNullOrWhiteSpace(child.ChildSuffix) || !suffixes.Add(child.ChildSuffix.Trim()))
            {
                errors.Add($"INVALID_CHILD_SUFFIX: Duplicate or empty child suffix '{child.ChildSuffix}'.");
            }

            if (child.AreaSqm <= 0m)
            {
                errors.Add($"INVALID_CHILD_AREA: Partition '{child.ChildSuffix}' must have positive area.");
            }

            totalChildArea += child.AreaSqm;
        }

        decimal allowedDelta = parentAreaSqm > 0m ? parentAreaSqm * AreaToleranceRatio : 0m;
        decimal delta = Math.Abs(totalChildArea - parentAreaSqm);

        if (parentAreaSqm > 0m && delta > allowedDelta)
        {
            errors.Add(
                $"AREA_CONSERVATION_VIOLATED: Sum of child areas ({totalChildArea:F4} sq.m) differs from parent ({parentAreaSqm:F4} sq.m) by {delta:F4} sq.m, exceeding +-0.1% ({allowedDelta:F4} sq.m).");
        }

        return new PartitionValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            ParentAreaSqm: parentAreaSqm,
            TotalChildAreaSqm: totalChildArea,
            AreaDeltaSqm: delta,
            AllowedToleranceSqm: allowedDelta);
    }
}

