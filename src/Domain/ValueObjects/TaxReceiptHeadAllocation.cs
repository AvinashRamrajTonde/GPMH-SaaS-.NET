using System.Text.Json;

namespace GPMH.Domain.ValueObjects;

/// <summary>
/// Immutable per-receipt FIFO head allocation snapshot capturing notice/warrant/penalty fees,
/// carried arrears, current FY demand breakdown, early rebate applied, and overpayment excess credit.
/// </summary>
public sealed record TaxReceiptHeadAllocation(
    decimal NoticeFeePaid = 0m,
    decimal WarrantFeePaid = 0m,
    decimal PenaltyPaid = 0m,
    decimal RebateApplied = 0m,
    DemandHeadBreakdown? ArrearsAllocated = null,
    DemandHeadBreakdown? CurrentAllocated = null,
    decimal ExcessCredit = 0m)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static readonly TaxReceiptHeadAllocation Empty = new(
        0m,
        0m,
        0m,
        0m,
        DemandHeadBreakdown.Empty,
        DemandHeadBreakdown.Empty,
        0m);

    public DemandHeadBreakdown SafeArrears => ArrearsAllocated ?? DemandHeadBreakdown.Empty;

    public DemandHeadBreakdown SafeCurrent => CurrentAllocated ?? DemandHeadBreakdown.Empty;

    public decimal TotalFeesPaid => NoticeFeePaid + WarrantFeePaid + PenaltyPaid + SafeArrears.FeesAndPenalty;

    public decimal TotalHeadsAllocated =>
        SafeArrears.GeneralTaxTotal + SafeArrears.WaterTotal +
        SafeCurrent.GeneralTaxTotal + SafeCurrent.WaterTotal;

    public decimal TotalWaterAllocated => SafeArrears.WaterTotal + SafeCurrent.WaterTotal;

    public decimal TotalAllocatedTowardDemand => TotalFeesPaid + TotalHeadsAllocated;

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static TaxReceiptHeadAllocation FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<TaxReceiptHeadAllocation>(json, SerializerOptions) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }
}

