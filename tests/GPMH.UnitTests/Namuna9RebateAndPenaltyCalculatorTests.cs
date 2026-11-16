using System.Reflection;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using Xunit;

namespace GPMH.UnitTests;

public sealed class Namuna9RebateAndPenaltyCalculatorTests
{
    [Fact]
    public void CalculateEarlyRebate_OnAndBeforeJuly31_Returns10PercentRoundedAwayFromZero()
    {
        // On July 30 (before cutoff)
        decimal onJuly30 = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
            eligibleCurrentGeneralTax: 246.00m,
            paymentOrApplyDate: new DateOnly(2026, 7, 30),
            assessmentStartYear: 2026,
            earlyRebatePct: 10.00m);

        Assert.Equal(24.60m, onJuly30);

        // On July 31 (inclusive statutory cutoff) with half-paise midpoint rounding test: 245.55 * 10% = 24.555 -> 24.56
        decimal midpointOnCutoff = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
            eligibleCurrentGeneralTax: 245.55m,
            paymentOrApplyDate: new DateOnly(2026, 7, 31),
            assessmentStartYear: 2026,
            earlyRebatePct: 10.00m);

        Assert.Equal(24.56m, midpointOnCutoff);
    }

    [Fact]
    public void CalculateEarlyRebate_AfterJuly31_ReturnsZero_UnlessCustomCutoffProvided()
    {
        decimal afterCutoff = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
            eligibleCurrentGeneralTax: 500.00m,
            paymentOrApplyDate: new DateOnly(2026, 8, 1),
            assessmentStartYear: 2026,
            earlyRebatePct: 10.00m);

        Assert.Equal(0m, afterCutoff);

        decimal withCustomCutoff = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
            eligibleCurrentGeneralTax: 500.00m,
            paymentOrApplyDate: new DateOnly(2026, 8, 15),
            assessmentStartYear: 2026,
            earlyRebatePct: 10.00m,
            overrideCutoffDate: new DateOnly(2026, 8, 31));

        Assert.Equal(50.00m, withCustomCutoff);
    }

    [Fact]
    public void CalculateEarlyRebate_AppliesOnlyOnCurrentGeneralTax_NeverArrearsOrPenalty()
    {
        // Only the 246 current general tax qualifies even if arrears = 1000 and water = 720
        decimal rebate = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
            eligibleCurrentGeneralTax: 246m,
            paymentOrApplyDate: new DateOnly(2026, 6, 15),
            assessmentStartYear: 2026,
            earlyRebatePct: 10.00m);

        Assert.Equal(24.60m, rebate);

        decimal penalty = Namuna9RebateAndPenaltyCalculator.CalculateLatePenalty(
            overduePrincipal: 1000.05m,
            latePenaltyPct: 10.00m);

        Assert.Equal(100.01m, penalty);
    }

    [Fact]
    public void ComputeRemaining_ConservesExactPendingBalanceAcrossHeadsAndFees()
    {
        DemandHeadBreakdown arrears = new(HouseTax: 66m, LightingTax: 40m, FeesAndPenalty: 15m);
        DemandHeadBreakdown current = new(HouseTax: 66m, LightingTax: 40m, HealthTax: 40m, SanitationTax: 100m);
        DemandCollectedBreakdown collected = new(NoticeFeePaid: 10m);

        decimal netPending = 121m + 246m + 10m + 25m + 20m - 24.60m - 10m; // 387.40
        DemandHeadBreakdown remaining = DemandLedgerCalculator.ComputeRemaining(
            arrears, current, collected, noticeFee: 10m, warrantFee: 25m, penalty: 20m, rebate: 24.60m, pendingBalance: netPending);

        Assert.Equal(netPending, remaining.Total);
        Assert.Equal(60m, remaining.FeesAndPenalty); // 15 carried + 10 + 25 + 20 - 10 paid
    }

    [Fact]
    public void StatutoryServices_UseDecimalOnly_NoFloatOrDouble()
    {
        Type[] types =
        [
            typeof(Namuna9RebateAndPenaltyCalculator),
            typeof(DemandLedgerCalculator),
            typeof(DemandHeadBreakdown),
            typeof(DemandCollectedBreakdown),
        ];

        foreach (Type t in types)
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.NotEqual(typeof(double), m.ReturnType);
                Assert.NotEqual(typeof(float), m.ReturnType);
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Assert.NotEqual(typeof(double), p.ParameterType);
                    Assert.NotEqual(typeof(float), p.ParameterType);
                }
            }
        }
    }
}

