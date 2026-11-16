using GPMH.Domain.ValueObjects;

namespace GPMH.Domain.Services;

/// <summary>Maps tax calculation heads onto the Namuna 9 per-head ledger structure.</summary>
public static class DemandHeadMapper
{
    public static DemandHeadBreakdown FromTaxHeads(
        decimal buildingTax,
        decimal openLandTax,
        decimal lightingTax,
        decimal healthTax,
        decimal sanitationTax,
        decimal toiletTax,
        decimal waterTax,
        decimal otherTax,
        string? waterConnectionCode)
    {
        bool special = string.Equals(waterConnectionCode, "SPECIAL", StringComparison.OrdinalIgnoreCase);

        return new DemandHeadBreakdown(
            HouseTax: buildingTax + openLandTax,
            LightingTax: lightingTax,
            HealthTax: healthTax,
            SanitationTax: sanitationTax,
            ToiletTax: toiletTax,
            GeneralWaterTax: special ? 0m : waterTax,
            SpecialWaterTax: special ? waterTax : 0m,
            OtherTax: otherTax);
    }
}

/// <summary>
/// Computes remaining (unpaid) per-head balances for the Namuna 9 recovery page and for FY rollover.
/// The returned breakdown always totals exactly the demand's authoritative <c>PendingBalance</c>.
/// </summary>
public static class DemandLedgerCalculator
{
    // Index layout: House, Lighting, Health, Sanitation, Toilet, GeneralWater, SpecialWater, Other, FeesAndPenalty
    private static readonly int[] GeneralOrder = [0, 1, 2, 3, 4, 7];
    private static readonly int[] WaterOrder = [5, 6];
    private const int FeesIndex = 8;

    public static DemandHeadBreakdown ComputeRemaining(
        DemandHeadBreakdown arrears,
        DemandHeadBreakdown current,
        DemandCollectedBreakdown collected,
        decimal noticeFee,
        decimal warrantFee,
        decimal penalty,
        decimal rebate,
        decimal pendingBalance)
    {
        ArgumentNullException.ThrowIfNull(arrears);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(collected);

        decimal[] a = Vector(arrears);
        decimal[] c = Vector(current);
        decimal[] p = Vector(collected.Heads);

        decimal[] rem = new decimal[9];
        for (int i = 0; i < 8; i++)
        {
            rem[i] = Math.Max(0m, a[i] + c[i] - p[i]);
        }

        rem[FeesIndex] = Math.Max(
            0m,
            arrears.FeesAndPenalty + noticeFee + warrantFee + penalty - collected.FeesPaid);

        decimal rebateLeft = Math.Max(0m, rebate);
        foreach (int idx in GeneralOrder)
        {
            decimal d = Math.Min(rebateLeft, rem[idx]);
            rem[idx] -= d;
            rebateLeft -= d;
        }

        decimal target = Math.Max(0m, pendingBalance);
        decimal total = rem.Sum();
        if (total > target)
        {
            decimal excess = total - target;
            foreach (int idx in GeneralOrder.Concat(WaterOrder).Append(FeesIndex))
            {
                decimal d = Math.Min(excess, rem[idx]);
                rem[idx] -= d;
                excess -= d;
                if (excess <= 0m)
                {
                    break;
                }
            }
        }
        else if (total < target)
        {
            rem[FeesIndex] += target - total;
        }

        return new DemandHeadBreakdown(
            rem[0], rem[1], rem[2], rem[3], rem[4], rem[5], rem[6], rem[7], rem[8]);
    }

    private static decimal[] Vector(DemandHeadBreakdown b) =>
    [
        b.HouseTax, b.LightingTax, b.HealthTax, b.SanitationTax,
        b.ToiletTax, b.GeneralWaterTax, b.SpecialWaterTax, b.OtherTax,
    ];
}

