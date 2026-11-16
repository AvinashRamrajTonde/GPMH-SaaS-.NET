using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Tenant-scoped annual Namuna 9 demand &amp; recovery ledger row linked to a property and assessment year.
/// Created as DraftAssessment upon Namuna 8 save and promoted to DemandPublished in Namuna 9.
/// Aggregate invariant:
/// TotalNetDemand = ArrearsBalance + CurrentDemand + NoticeFee + WarrantFee + PenaltyAmount - RebateAmount;
/// PendingBalance = max(0, TotalNetDemand - PaidAmount).
/// </summary>
public sealed class PropertyDemand : BaseEntity, ITenantEntity
{
    public const string StatusDraft = "DraftAssessment";
    public const string StatusPublished = "DemandPublished";
    public const string StatusPartiallyPaid = "PartiallyPaid";
    public const string StatusSettled = "Settled";

    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid AssessmentYearId { get; set; }
    public decimal ArrearsBalance { get; set; }
    public decimal CurrentDemand { get; set; }
    public decimal RebateAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingBalance { get; set; }
    public string Status { get; set; } = StatusDraft;

    // Sprint 4 — Namuna 9 statutory charges & lifecycle metadata
    public decimal NoticeFee { get; set; }
    public decimal WarrantFee { get; set; }
    public bool EarlyRebateEligible { get; set; }
    public bool EarlyRebateApplied { get; set; }
    public DateOnly? RebateCutoffDate { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? DemandRemarks { get; set; }

    // Per-tax-head JSON snapshots (see DemandHeadBreakdown / DemandCollectedBreakdown)
    public string ArrearsByHeadJson { get; set; } = "{}";
    public string CurrentByHeadJson { get; set; } = "{}";
    public string CollectedByHeadJson { get; set; } = "{}";

    public decimal TotalNetDemand =>
        ArrearsBalance + CurrentDemand + NoticeFee + WarrantFee + PenaltyAmount - RebateAmount;

    /// <summary>Recomputes <see cref="PendingBalance"/> from the aggregate invariant.</summary>
    public void RecalculatePendingBalance() =>
        PendingBalance = Math.Max(0m, TotalNetDemand - PaidAmount);
}
