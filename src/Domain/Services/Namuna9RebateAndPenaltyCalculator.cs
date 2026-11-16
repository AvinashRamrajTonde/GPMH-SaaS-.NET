namespace GPMH.Domain.Services;

/// <summary>
/// Pure decimal Namuna 9 early-payment rebate and late-penalty calculator (ADR-016: no float/double).
/// </summary>
public static class Namuna9RebateAndPenaltyCalculator
{
    public const decimal DefaultEarlyRebatePct = 10.00m;
    public const decimal DefaultLatePenaltyPct = 10.00m;
    public const int DefaultNoticeGraceDays = 15;

    /// <summary>Default statutory cut-off: 31 July of the assessment start year.</summary>
    public static DateOnly DefaultRebateCutoff(int assessmentStartYear) => new(assessmentStartYear, 7, 31);

    /// <summary>
    /// Early rebate on the eligible CURRENT general tax only. Returns 0 after the cut-off date.
    /// Arrears and penalty are never discounted.
    /// </summary>
    public static decimal CalculateEarlyRebate(
        decimal eligibleCurrentGeneralTax,
        DateOnly paymentOrApplyDate,
        int assessmentStartYear,
        decimal earlyRebatePct,
        DateOnly? overrideCutoffDate = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eligibleCurrentGeneralTax);
        ArgumentOutOfRangeException.ThrowIfNegative(earlyRebatePct);

        DateOnly cutoff = overrideCutoffDate ?? DefaultRebateCutoff(assessmentStartYear);
        if (paymentOrApplyDate > cutoff)
        {
            return 0m;
        }

        return Math.Round(
            eligibleCurrentGeneralTax * (earlyRebatePct / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateLatePenalty(decimal overduePrincipal, decimal latePenaltyPct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(overduePrincipal);
        ArgumentOutOfRangeException.ThrowIfNegative(latePenaltyPct);

        return Math.Round(
            overduePrincipal * (latePenaltyPct / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }
}

