using GPMH.Domain.Services;
using Xunit;

namespace GPMH.UnitTests;

public sealed class FifoPaymentAllocatorTests
{
    [Fact]
    public void Allocate_FollowsStrictStatutoryFifoPriorityAndConservesTotal()
    {
        FifoPaymentRequest request = new(
            AmountPaid: 650m,
            NoticeFeeDue: 25m,
            WarrantFeeDue: 15m,
            PenaltyFeeDue: 60m,
            DemandBuckets:
            [
                // Intentionally unsorted input order to prove StartYear ascending ordering for arrears, CurrentYear last
                new DemandBucketInput(
                    StartYear: 2026,
                    YearLabel: "2026-27",
                    IsCurrentYear: true,
                    DueBalances: new TaxHeadBalances(BuildingAndOpenLand: 200m, Water: 200m)),
                new DemandBucketInput(
                    StartYear: 2024,
                    YearLabel: "2024-25",
                    IsCurrentYear: false,
                    DueBalances: new TaxHeadBalances(BuildingAndOpenLand: 120m, Lighting: 40m, Sanitation: 40m)),
                new DemandBucketInput(
                    StartYear: 2025,
                    YearLabel: "2025-26",
                    IsCurrentYear: false,
                    DueBalances: new TaxHeadBalances(BuildingAndOpenLand: 100m, Health: 50m)),
            ]);

        FifoPaymentResult result = FifoPaymentAllocator.Allocate(request);

        // Fees cleared first: 25 + 15 + 60 = 100 (remaining = 550)
        Assert.Equal(25m, result.NoticeFeePaid);
        Assert.Equal(15m, result.WarrantFeePaid);
        Assert.Equal(60m, result.PenaltyFeePaid);

        // 2024-25 oldest arrears cleared in full: 120 + 40 + 40 = 200 (remaining = 350)
        Assert.Equal("2024-25", result.BucketAllocations[0].YearLabel);
        Assert.Equal(200m, result.BucketAllocations[0].AllocatedHeads.Total);
        Assert.Equal(0m, result.BucketAllocations[0].RemainingHeads.Total);

        // 2025-26 arrears cleared in full: 100 + 50 = 150 (remaining = 200)
        Assert.Equal("2025-26", result.BucketAllocations[1].YearLabel);
        Assert.Equal(150m, result.BucketAllocations[1].AllocatedHeads.Total);
        Assert.Equal(0m, result.BucketAllocations[1].RemainingHeads.Total);

        // 2026-27 current year receives remaining 200 on BuildingAndOpenLand; Water 200 still due
        Assert.Equal("2026-27", result.BucketAllocations[2].YearLabel);
        Assert.Equal(200m, result.BucketAllocations[2].AllocatedHeads.BuildingAndOpenLand);
        Assert.Equal(200m, result.BucketAllocations[2].RemainingHeads.Water);
        Assert.Equal(0m, result.ExcessCredit);
    }

    [Fact]
    public void Allocate_OverpaymentBecomesExplicitExcessCredit()
    {
        FifoPaymentRequest request = new(
            AmountPaid: 500m,
            NoticeFeeDue: 0m,
            WarrantFeeDue: 0m,
            PenaltyFeeDue: 0m,
            DemandBuckets:
            [
                new DemandBucketInput(2026, "2026-27", true, new TaxHeadBalances(BuildingAndOpenLand: 246m)),
            ]);

        FifoPaymentResult result = FifoPaymentAllocator.Allocate(request);

        Assert.Equal(246m, result.BucketAllocations[0].AllocatedHeads.Total);
        Assert.Equal(254m, result.ExcessCredit);
    }
}

