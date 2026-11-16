namespace GPMH.Domain.ValueObjects;

/// <summary>
/// Immutable itemized snapshot captured inside <c>PropertyDemandBill.HeadBreakdownJson</c> at bill issue time.
/// Enables idempotent batch regeneration checks and historical bill rendering even after future demand changes.
/// </summary>
public sealed record BillHeadSnapshot(
    DemandHeadBreakdown Arrears,
    DemandHeadBreakdown Current,
    DemandCollectedBreakdown Collected,
    decimal NoticeFee,
    decimal WarrantFee,
    decimal PenaltyAmount,
    decimal RebateAmount,
    decimal PaidAmount,
    decimal NetPayableAmount)
{
    public string ToJson() => DemandHeadJson.Serialize(this);

    public static BillHeadSnapshot? FromJson(string? json) =>
        DemandHeadJson.Deserialize<BillHeadSnapshot>(json);

    public bool MatchesAmounts(
        DemandHeadBreakdown arrears,
        DemandHeadBreakdown current,
        decimal noticeFee,
        decimal warrantFee,
        decimal penaltyAmount,
        decimal rebateAmount,
        decimal netPayableAmount) =>
        Arrears == arrears &&
        Current == current &&
        NoticeFee == noticeFee &&
        WarrantFee == warrantFee &&
        PenaltyAmount == penaltyAmount &&
        RebateAmount == rebateAmount &&
        NetPayableAmount == netPayableAmount;
}
