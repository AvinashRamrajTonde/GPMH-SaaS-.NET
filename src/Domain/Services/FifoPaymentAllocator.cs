namespace GPMH.Domain.Services;

public sealed record TaxHeadBalances(
    decimal BuildingAndOpenLand = 0m,
    decimal Lighting = 0m,
    decimal Health = 0m,
    decimal Sanitation = 0m,
    decimal Toilet = 0m,
    decimal Water = 0m,
    decimal Other = 0m)
{
    public decimal Total => BuildingAndOpenLand + Lighting + Health + Sanitation + Toilet + Water + Other;
}

public sealed record DemandBucketInput(
    int StartYear,
    string YearLabel,
    bool IsCurrentYear,
    TaxHeadBalances DueBalances);

public sealed record FifoPaymentRequest(
    decimal AmountPaid,
    decimal NoticeFeeDue,
    decimal WarrantFeeDue,
    decimal PenaltyFeeDue,
    IReadOnlyList<DemandBucketInput> DemandBuckets);

public sealed record DemandBucketAllocation(
    int StartYear,
    string YearLabel,
    bool IsCurrentYear,
    TaxHeadBalances AllocatedHeads,
    TaxHeadBalances RemainingHeads);

public sealed record FifoPaymentResult(
    decimal NoticeFeePaid,
    decimal WarrantFeePaid,
    decimal PenaltyFeePaid,
    IReadOnlyList<DemandBucketAllocation> BucketAllocations,
    decimal ExcessCredit);

/// <summary>
/// Strict statutory FIFO allocator for Namuna 10 tax receipts.
/// Priority order: Notice/Warrant/Penalty Fees -> Oldest Arrears (FY ascending) -> Current Year -> Excess Credit.
/// </summary>
public static class FifoPaymentAllocator
{
    public static FifoPaymentResult Allocate(FifoPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.AmountPaid);
        ArgumentOutOfRangeException.ThrowIfNegative(request.NoticeFeeDue);
        ArgumentOutOfRangeException.ThrowIfNegative(request.WarrantFeeDue);
        ArgumentOutOfRangeException.ThrowIfNegative(request.PenaltyFeeDue);

        decimal remaining = request.AmountPaid;

        decimal noticePaid = Consume(ref remaining, request.NoticeFeeDue);
        decimal warrantPaid = Consume(ref remaining, request.WarrantFeeDue);
        decimal penaltyPaid = Consume(ref remaining, request.PenaltyFeeDue);

        IReadOnlyList<DemandBucketInput> orderedBuckets = request.DemandBuckets
            .OrderBy(b => b.IsCurrentYear ? 1 : 0)
            .ThenBy(b => b.StartYear)
            .ThenBy(b => b.YearLabel, StringComparer.Ordinal)
            .ToArray();

        List<DemandBucketAllocation> allocations = new(orderedBuckets.Count);

        foreach (DemandBucketInput bucket in orderedBuckets)
        {
            ValidateHeadsNonNegative(bucket.DueBalances);

            decimal bldgPaid = Consume(ref remaining, bucket.DueBalances.BuildingAndOpenLand);
            decimal lightPaid = Consume(ref remaining, bucket.DueBalances.Lighting);
            decimal healthPaid = Consume(ref remaining, bucket.DueBalances.Health);
            decimal sanPaid = Consume(ref remaining, bucket.DueBalances.Sanitation);
            decimal toiletPaid = Consume(ref remaining, bucket.DueBalances.Toilet);
            decimal waterPaid = Consume(ref remaining, bucket.DueBalances.Water);
            decimal otherPaid = Consume(ref remaining, bucket.DueBalances.Other);

            TaxHeadBalances allocated = new(
                BuildingAndOpenLand: bldgPaid,
                Lighting: lightPaid,
                Health: healthPaid,
                Sanitation: sanPaid,
                Toilet: toiletPaid,
                Water: waterPaid,
                Other: otherPaid);

            TaxHeadBalances left = new(
                BuildingAndOpenLand: bucket.DueBalances.BuildingAndOpenLand - bldgPaid,
                Lighting: bucket.DueBalances.Lighting - lightPaid,
                Health: bucket.DueBalances.Health - healthPaid,
                Sanitation: bucket.DueBalances.Sanitation - sanPaid,
                Toilet: bucket.DueBalances.Toilet - toiletPaid,
                Water: bucket.DueBalances.Water - waterPaid,
                Other: bucket.DueBalances.Other - otherPaid);

            allocations.Add(new DemandBucketAllocation(
                bucket.StartYear,
                bucket.YearLabel,
                bucket.IsCurrentYear,
                allocated,
                left));
        }

        return new FifoPaymentResult(
            NoticeFeePaid: noticePaid,
            WarrantFeePaid: warrantPaid,
            PenaltyFeePaid: penaltyPaid,
            BucketAllocations: allocations,
            ExcessCredit: remaining);
    }

    private static decimal Consume(ref decimal available, decimal due)
    {
        if (available <= 0m || due <= 0m)
        {
            return 0m;
        }

        decimal applied = Math.Min(available, due);
        available -= applied;
        return applied;
    }

    private static void ValidateHeadsNonNegative(TaxHeadBalances b)
    {
        if (b.BuildingAndOpenLand < 0m || b.Lighting < 0m || b.Health < 0m ||
            b.Sanitation < 0m || b.Toilet < 0m || b.Water < 0m || b.Other < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Due tax head balances cannot be negative.");
        }
    }
}

