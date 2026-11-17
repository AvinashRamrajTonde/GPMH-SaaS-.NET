using GPMH.Domain.ValueObjects;

namespace GPMH.Domain.Services;

/// <summary>Exact unpaid balances per bucket derived from a <c>PropertyDemand</c>.</summary>
public sealed record Namuna10DueBreakdown(
    decimal NoticeFeeDue,
    decimal WarrantFeeDue,
    decimal PenaltyDue,
    decimal CarriedFeesDue,
    DemandHeadBreakdown ArrearsDue,
    DemandHeadBreakdown CurrentDue)
{
    public decimal TotalDue =>
        NoticeFeeDue + WarrantFeeDue + PenaltyDue + CarriedFeesDue + ArrearsDue.Total + CurrentDue.Total;
}

/// <summary>
/// Result of allocating a tax collection strictly through <see cref="FifoPaymentAllocator"/>,
/// returning the immutable receipt head snapshot and updated cumulative collected totals.
/// </summary>
public sealed record Namuna10AllocationOutcome(
    TaxReceiptHeadAllocation ReceiptAllocation,
    DemandCollectedBreakdown UpdatedCollected,
    decimal AppliedTowardDemand,
    decimal ExcessCredit);

/// <summary>
/// Bridges Namuna 9 per-head demand breakdowns (<see cref="DemandHeadBreakdown"/> with split water heads)
/// into <see cref="FifoPaymentAllocator"/> and maps FIFO results cleanly back to receipt &amp; demand ledgers.
/// </summary>
public static class Namuna10FifoBridge
{
    public static Namuna10DueBreakdown ComputeRemainingDues(
        DemandHeadBreakdown arrears,
        DemandHeadBreakdown current,
        DemandCollectedBreakdown collected,
        decimal noticeFee,
        decimal warrantFee,
        decimal penaltyAmount,
        decimal rebateAmount,
        decimal pendingBalance)
    {
        ArgumentNullException.ThrowIfNull(arrears);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(collected);

        decimal noticePaidBefore = Math.Min(noticeFee, Math.Max(0m, collected.NoticeFeePaid));
        decimal noticeDue = Math.Max(0m, noticeFee - noticePaidBefore);

        decimal warrantPaidBefore = Math.Min(warrantFee, Math.Max(0m, collected.WarrantFeePaid));
        decimal warrantDue = Math.Max(0m, warrantFee - warrantPaidBefore);

        decimal surplusFeesPaid = Math.Max(0m, collected.FeesPaid - noticePaidBefore - warrantPaidBefore);
        decimal paidCarriedFees = Math.Min(arrears.FeesAndPenalty, surplusFeesPaid);
        decimal carriedFeesDue = Math.Max(0m, arrears.FeesAndPenalty - paidCarriedFees);

        decimal paidCurrentPenalty = Math.Max(0m, surplusFeesPaid - paidCarriedFees);
        decimal penaltyDue = Math.Max(0m, penaltyAmount - paidCurrentPenalty);

        decimal[] arrVec = ToVector(arrears);
        decimal[] curNet = ToVector(current);
        decimal[] colVec = ToVector(collected.Heads);

        // Deduct early rebate strictly from current-year general tax heads (never arrears/water/fees)
        decimal rebateLeft = Math.Max(0m, rebateAmount);
        int[] generalOrder = [0, 1, 2, 3, 4, 7];
        foreach (int idx in generalOrder)
        {
            decimal d = Math.Min(rebateLeft, curNet[idx]);
            curNet[idx] -= d;
            rebateLeft -= d;
        }

        decimal[] remArr = new decimal[8];
        decimal[] remCur = new decimal[8];
        for (int i = 0; i < 8; i++)
        {
            decimal paidOnArr = Math.Min(arrVec[i], Math.Max(0m, colVec[i]));
            remArr[i] = Math.Max(0m, arrVec[i] - paidOnArr);

            decimal paidOnCur = Math.Max(0m, colVec[i] - arrVec[i]);
            remCur[i] = Math.Max(0m, curNet[i] - paidOnCur);
        }

        decimal targetPending = Math.Max(0m, pendingBalance);
        decimal computedTotal = noticeDue + warrantDue + carriedFeesDue + penaltyDue + remArr.Sum() + remCur.Sum();

        if (computedTotal > targetPending)
        {
            decimal diff = computedTotal - targetPending;
            diff = TrimVector(remCur, diff);
            diff = TrimVector(remArr, diff);

            if (diff > 0m)
            {
                decimal d = Math.Min(diff, penaltyDue);
                penaltyDue -= d;
                diff -= d;
            }

            if (diff > 0m)
            {
                decimal d = Math.Min(diff, carriedFeesDue);
                carriedFeesDue -= d;
                diff -= d;
            }

            if (diff > 0m)
            {
                decimal d = Math.Min(diff, warrantDue);
                warrantDue -= d;
                diff -= d;
            }

            if (diff > 0m)
            {
                noticeDue = Math.Max(0m, noticeDue - diff);
            }
        }
        else if (computedTotal < targetPending)
        {
            remCur[7] += targetPending - computedTotal;
        }

        return new Namuna10DueBreakdown(
            NoticeFeeDue: noticeDue,
            WarrantFeeDue: warrantDue,
            PenaltyDue: penaltyDue,
            CarriedFeesDue: carriedFeesDue,
            ArrearsDue: FromVector(remArr, feesAndPenalty: 0m),
            CurrentDue: FromVector(remCur, feesAndPenalty: 0m));
    }

    public static Namuna10AllocationOutcome AllocatePayment(
        decimal amountPaid,
        int startYear,
        string yearLabel,
        Namuna10DueBreakdown dueBreakdown,
        DemandCollectedBreakdown previouslyCollected,
        decimal newlyGrantedRebate = 0m,
        decimal? totalDemandRebateAfter = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountPaid);
        ArgumentNullException.ThrowIfNull(dueBreakdown);
        ArgumentNullException.ThrowIfNull(previouslyCollected);

        List<DemandBucketInput> buckets = [];
        if (dueBreakdown.ArrearsDue.Total > 0m)
        {
            buckets.Add(new DemandBucketInput(
                StartYear: startYear - 1,
                YearLabel: $"{startYear - 1} Arrears",
                IsCurrentYear: false,
                DueBalances: ToTaxHeadBalances(dueBreakdown.ArrearsDue)));
        }

        if (dueBreakdown.CurrentDue.Total > 0m)
        {
            buckets.Add(new DemandBucketInput(
                StartYear: startYear,
                YearLabel: yearLabel,
                IsCurrentYear: true,
                DueBalances: ToTaxHeadBalances(dueBreakdown.CurrentDue)));
        }

        FifoPaymentResult fifo = FifoPaymentAllocator.Allocate(new FifoPaymentRequest(
            AmountPaid: amountPaid,
            NoticeFeeDue: dueBreakdown.NoticeFeeDue,
            WarrantFeeDue: dueBreakdown.WarrantFeeDue,
            PenaltyFeeDue: dueBreakdown.CarriedFeesDue + dueBreakdown.PenaltyDue,
            DemandBuckets: buckets));

        // Split PenaltyFeePaid back into carried prior-year fees (first) and current-year penalty
        decimal carriedFeesPaidNow = Math.Min(dueBreakdown.CarriedFeesDue, fifo.PenaltyFeePaid);
        decimal currentPenaltyPaidNow = fifo.PenaltyFeePaid - carriedFeesPaidNow;

        DemandBucketAllocation? arrBucket = fifo.BucketAllocations.FirstOrDefault(b => !b.IsCurrentYear);
        DemandBucketAllocation? curBucket = fifo.BucketAllocations.FirstOrDefault(b => b.IsCurrentYear);

        DemandHeadBreakdown arrAllocated = MapBucketToHeads(
            arrBucket?.AllocatedHeads,
            dueBreakdown.ArrearsDue,
            carriedFeesPaidNow);

        DemandHeadBreakdown curAllocated = MapBucketToHeads(
            curBucket?.AllocatedHeads,
            dueBreakdown.CurrentDue,
            feesAndPenalty: 0m);

        TaxReceiptHeadAllocation receiptAlloc = new(
            NoticeFeePaid: fifo.NoticeFeePaid,
            WarrantFeePaid: fifo.WarrantFeePaid,
            PenaltyPaid: currentPenaltyPaidNow,
            RebateApplied: Math.Max(0m, newlyGrantedRebate),
            ArrearsAllocated: arrAllocated,
            CurrentAllocated: curAllocated,
            ExcessCredit: fifo.ExcessCredit);

        DemandCollectedBreakdown updatedCollected = new(
            HouseTax: previouslyCollected.HouseTax + arrAllocated.HouseTax + curAllocated.HouseTax,
            LightingTax: previouslyCollected.LightingTax + arrAllocated.LightingTax + curAllocated.LightingTax,
            HealthTax: previouslyCollected.HealthTax + arrAllocated.HealthTax + curAllocated.HealthTax,
            SanitationTax: previouslyCollected.SanitationTax + arrAllocated.SanitationTax + curAllocated.SanitationTax,
            ToiletTax: previouslyCollected.ToiletTax + arrAllocated.ToiletTax + curAllocated.ToiletTax,
            GeneralWaterTax: previouslyCollected.GeneralWaterTax + arrAllocated.GeneralWaterTax + curAllocated.GeneralWaterTax,
            SpecialWaterTax: previouslyCollected.SpecialWaterTax + arrAllocated.SpecialWaterTax + curAllocated.SpecialWaterTax,
            OtherTax: previouslyCollected.OtherTax + arrAllocated.OtherTax + curAllocated.OtherTax,
            NoticeFeePaid: previouslyCollected.NoticeFeePaid + fifo.NoticeFeePaid,
            WarrantFeePaid: previouslyCollected.WarrantFeePaid + fifo.WarrantFeePaid,
            PenaltyPaid: previouslyCollected.PenaltyPaid + fifo.PenaltyFeePaid,
            RebateGranted: totalDemandRebateAfter ?? (previouslyCollected.RebateGranted + Math.Max(0m, newlyGrantedRebate)));

        decimal appliedTowardDemand = receiptAlloc.TotalAllocatedTowardDemand;

        return new Namuna10AllocationOutcome(
            ReceiptAllocation: receiptAlloc,
            UpdatedCollected: updatedCollected,
            AppliedTowardDemand: appliedTowardDemand,
            ExcessCredit: fifo.ExcessCredit);
    }

    /// <summary>
    /// Exact statutory inverse of <see cref="AllocatePayment"/> used when a Gramsevak cancels a receipt:
    /// subtracts each allocated head, fee, penalty, and receipt-specific rebate, clamped at zero.
    /// </summary>
    public static DemandCollectedBreakdown ReverseAllocation(
        DemandCollectedBreakdown existingCollected,
        TaxReceiptHeadAllocation allocationToReverse,
        decimal discountGiven = 0m)
    {
        ArgumentNullException.ThrowIfNull(existingCollected);
        ArgumentNullException.ThrowIfNull(allocationToReverse);

        DemandHeadBreakdown arr = allocationToReverse.ArrearsAllocated ?? DemandHeadBreakdown.Empty;
        DemandHeadBreakdown cur = allocationToReverse.CurrentAllocated ?? DemandHeadBreakdown.Empty;

        decimal penaltyFromReceipt = allocationToReverse.PenaltyPaid + arr.FeesAndPenalty;
        decimal rebateToReverse = Math.Max(discountGiven, allocationToReverse.RebateApplied);

        return new DemandCollectedBreakdown(
            HouseTax: ClampSub(existingCollected.HouseTax, arr.HouseTax + cur.HouseTax),
            LightingTax: ClampSub(existingCollected.LightingTax, arr.LightingTax + cur.LightingTax),
            HealthTax: ClampSub(existingCollected.HealthTax, arr.HealthTax + cur.HealthTax),
            SanitationTax: ClampSub(existingCollected.SanitationTax, arr.SanitationTax + cur.SanitationTax),
            ToiletTax: ClampSub(existingCollected.ToiletTax, arr.ToiletTax + cur.ToiletTax),
            GeneralWaterTax: ClampSub(existingCollected.GeneralWaterTax, arr.GeneralWaterTax + cur.GeneralWaterTax),
            SpecialWaterTax: ClampSub(existingCollected.SpecialWaterTax, arr.SpecialWaterTax + cur.SpecialWaterTax),
            OtherTax: ClampSub(existingCollected.OtherTax, arr.OtherTax + cur.OtherTax),
            NoticeFeePaid: ClampSub(existingCollected.NoticeFeePaid, allocationToReverse.NoticeFeePaid),
            WarrantFeePaid: ClampSub(existingCollected.WarrantFeePaid, allocationToReverse.WarrantFeePaid),
            PenaltyPaid: ClampSub(existingCollected.PenaltyPaid, penaltyFromReceipt),
            RebateGranted: ClampSub(existingCollected.RebateGranted, rebateToReverse));
    }

    private static decimal ClampSub(decimal current, decimal delta) =>
        Math.Max(0m, Math.Round(current - Math.Max(0m, delta), 2, MidpointRounding.AwayFromZero));

    private static TaxHeadBalances ToTaxHeadBalances(DemandHeadBreakdown b) =>
        new(
            BuildingAndOpenLand: b.HouseTax,
            Lighting: b.LightingTax,
            Health: b.HealthTax,
            Sanitation: b.SanitationTax,
            Toilet: b.ToiletTax,
            Water: b.GeneralWaterTax + b.SpecialWaterTax,
            Other: b.OtherTax);

    private static DemandHeadBreakdown MapBucketToHeads(
        TaxHeadBalances? allocated,
        DemandHeadBreakdown dueForWaterSplit,
        decimal feesAndPenalty)
    {
        if (allocated is null)
        {
            return feesAndPenalty > 0m ? new DemandHeadBreakdown(FeesAndPenalty: feesAndPenalty) : DemandHeadBreakdown.Empty;
        }

        decimal genWaterPaid = Math.Min(allocated.Water, dueForWaterSplit.GeneralWaterTax);
        decimal specWaterPaid = Math.Max(0m, allocated.Water - genWaterPaid);

        return new DemandHeadBreakdown(
            HouseTax: allocated.BuildingAndOpenLand,
            LightingTax: allocated.Lighting,
            HealthTax: allocated.Health,
            SanitationTax: allocated.Sanitation,
            ToiletTax: allocated.Toilet,
            GeneralWaterTax: genWaterPaid,
            SpecialWaterTax: specWaterPaid,
            OtherTax: allocated.Other,
            FeesAndPenalty: feesAndPenalty);
    }

    private static decimal TrimVector(decimal[] vec, decimal remainingDiff)
    {
        for (int i = vec.Length - 1; i >= 0 && remainingDiff > 0m; i--)
        {
            decimal d = Math.Min(remainingDiff, vec[i]);
            vec[i] -= d;
            remainingDiff -= d;
        }

        return remainingDiff;
    }

    private static decimal[] ToVector(DemandHeadBreakdown b) =>
    [
        b.HouseTax,
        b.LightingTax,
        b.HealthTax,
        b.SanitationTax,
        b.ToiletTax,
        b.GeneralWaterTax,
        b.SpecialWaterTax,
        b.OtherTax,
    ];

    private static DemandHeadBreakdown FromVector(decimal[] v, decimal feesAndPenalty) =>
        new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], feesAndPenalty);
}

