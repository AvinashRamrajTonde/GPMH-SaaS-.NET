using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Statutory Namuna 9-Ka tax demand bill &amp; 15-day notice record (नमुना ९-क कर मागणी पावती / बिल)
/// snapshotting itemized arrears/current dues, dynamic NPCI UPI payload, and gap-free bill sequence.
/// </summary>
public sealed class PropertyDemandBill : BaseEntity, ITenantEntity
{
    public const string StatusIssued = "Issued";
    public const string StatusPartiallyPaid = "PartiallyPaid";
    public const string StatusPaid = "Paid";
    public const string StatusSuperseded = "Superseded";

    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid PropertyDemandId { get; set; }
    public Guid AssessmentYearId { get; set; }
    public required string BillNo { get; set; }
    public long BillSequence { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? RebateCutoffDate { get; set; }
    public decimal ArrearsAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal NoticeAndPenaltyAmount { get; set; }
    public decimal RebateAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public string HeadBreakdownJson { get; set; } = "{}";
    public string? UpiIdUsed { get; set; }
    public string? UpiPayUri { get; set; }
    public string Status { get; set; } = StatusIssued;
    public Guid IssuedByUserId { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
}

