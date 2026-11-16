using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using Xunit;

namespace GPMH.UnitTests;

public sealed class Namuna10FifoBridgeTests
{
    [Fact]
    public void Bridge_PartialPayment_ClearsCarriedFeesThenArrearsThenCurrent_WithExactWaterSplitConservation()
    {
        DemandHeadBreakdown arrears = new(
            HouseTax: 66m,
            LightingTax: 40m,
            GeneralWaterTax: 300m,
            SpecialWaterTax: 200m,
            FeesAndPenalty: 55m);

        DemandHeadBreakdown current = new(
            HouseTax: 66m,
            LightingTax: 40m,
            HealthTax: 40m,
            SanitationTax: 100m,
            GeneralWaterTax: 720m);

        DemandCollectedBreakdown previouslyCollected = DemandCollectedBreakdown.Empty;
        decimal pendingBalance = arrears.Total + current.Total; // 661 + 966 = 1627m

        Namuna10DueBreakdown dues = Namuna10FifoBridge.ComputeRemainingDues(
            arrears: arrears,
            current: current,
            collected: previouslyCollected,
            noticeFee: 10m,
            warrantFee: 0m,
            penaltyAmount: 20m,
            rebateAmount: 0m,
            pendingBalance: pendingBalance + 30m);

        Assert.Equal(10m, dues.NoticeFeeDue);
        Assert.Equal(20m, dues.PenaltyDue);
        Assert.Equal(55m, dues.CarriedFeesDue);
        Assert.Equal(1657m, dues.TotalDue);

        // Pay ₹500 -> clears Notice (10) + CarriedFees (55) + CurrentPenalty (20) = 85 Fees, leaving 415 for Arrears:
        // House (66) + Lighting (40) = 106 -> remaining 309 -> GeneralWater (300) + SpecialWater (9)!
        Namuna10AllocationOutcome outcome = Namuna10FifoBridge.AllocatePayment(
            amountPaid: 500m,
            startYear: 2027,
            yearLabel: "2027-28",
            dueBreakdown: dues,
            previouslyCollected: previouslyCollected);

        Assert.Equal(500m, outcome.AppliedTowardDemand);
        Assert.Equal(0m, outcome.ExcessCredit);
        Assert.Equal(10m, outcome.ReceiptAllocation.NoticeFeePaid);
        Assert.Equal(20m, outcome.ReceiptAllocation.PenaltyPaid);
        Assert.Equal(55m, outcome.ReceiptAllocation.SafeArrears.FeesAndPenalty);
        Assert.Equal(66m, outcome.ReceiptAllocation.SafeArrears.HouseTax);
        Assert.Equal(40m, outcome.ReceiptAllocation.SafeArrears.LightingTax);
        Assert.Equal(300m, outcome.ReceiptAllocation.SafeArrears.GeneralWaterTax);
        Assert.Equal(9m, outcome.ReceiptAllocation.SafeArrears.SpecialWaterTax);
        Assert.Equal(0m, outcome.ReceiptAllocation.SafeCurrent.Total);

        // Verify DemandLedgerCalculator matches remaining 1657 - 500 = 1157m head-for-head
        DemandHeadBreakdown remLedger = DemandLedgerCalculator.ComputeRemaining(
            arrears, current, outcome.UpdatedCollected,
            noticeFee: 10m, warrantFee: 0m, penalty: 20m, rebate: 0m, pendingBalance: 1157m);

        Assert.Equal(0m, remLedger.HouseTax - 66m); // Arrears house cleared, only current 66 left
        Assert.Equal(191m, remLedger.SpecialWaterTax - 0m); // 200 - 9 = 191 arrears special water left
        Assert.Equal(720m, remLedger.GeneralWaterTax); // Arrears 300 cleared, current 720 left
        Assert.Equal(1157m, remLedger.Total);
    }

    [Fact]
    public void Bridge_SecondInstallmentAndOverpayment_CapturesExcessCreditWithoutOverApplyingDemand()
    {
        DemandHeadBreakdown current = new(HouseTax: 66m, LightingTax: 40m, HealthTax: 40m, SanitationTax: 100m);
        decimal rebate = 24.60m; // 10% early rebate on 246 -> remaining net = 221.40

        Namuna10DueBreakdown dues = Namuna10FifoBridge.ComputeRemainingDues(
            arrears: DemandHeadBreakdown.Empty,
            current: current,
            collected: DemandCollectedBreakdown.Empty,
            noticeFee: 0m,
            warrantFee: 0m,
            penaltyAmount: 0m,
            rebateAmount: rebate,
            pendingBalance: 221.40m);

        Assert.Equal(221.40m, dues.TotalDue);

        Namuna10AllocationOutcome outcome = Namuna10FifoBridge.AllocatePayment(
            amountPaid: 300.00m,
            startYear: 2026,
            yearLabel: "2026-27",
            dueBreakdown: dues,
            previouslyCollected: DemandCollectedBreakdown.Empty,
            newlyGrantedRebate: rebate,
            totalDemandRebateAfter: rebate);

        Assert.Equal(221.40m, outcome.AppliedTowardDemand);
        Assert.Equal(78.60m, outcome.ExcessCredit);
        Assert.Equal(221.40m, outcome.UpdatedCollected.Total);
        Assert.Equal(rebate, outcome.UpdatedCollected.RebateGranted);
    }
}

