using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Statutory Namuna 8 ownership mutation / transfer register entry (फेरफार नोंदवही)
/// tracking 15-day public notice period, objections, private supporting deeds, and GP resolution approval.
/// </summary>
public sealed class PropertyMutation : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public required string ApplicationNo { get; set; }
    public required string MutationType { get; set; } // Heirship, SaleDeed, GiftDeed, ReleaseDeed, CourtDecree, Partition
    public required string PreviousOwnersSnapshotJson { get; set; }
    public required string ProposedOwnersJson { get; set; }
    public string? DocumentReferenceNo { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public string? Remarks { get; set; }
    public string? SupportingAssetIdsJson { get; set; }

    // 15-day statutory public notice tracking
    public DateTimeOffset NoticeIssuedAtUtc { get; set; }
    public DateTimeOffset NoticeEndsAtUtc { get; set; }
    public bool HasObjection { get; set; }
    public string? ObjectionSummary { get; set; }
    public Guid? ObjectionRecordedByUserId { get; set; }
    public DateTimeOffset? ObjectionRecordedAtUtc { get; set; }

    // GP monthly resolution approval / rejection
    public string? ResolutionNo { get; set; }
    public DateOnly? ResolutionDate { get; set; }
    public string Status { get; set; } = "NoticePublished"; // NoticePublished, ObjectionRecorded, Approved, Rejected
    public string? RejectionReason { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public bool IsFinalized() =>
        string.Equals(Status, "Approved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Rejected", StringComparison.OrdinalIgnoreCase);

    public bool IsNoticeWindowElapsed(DateTimeOffset utcNow) => utcNow >= NoticeEndsAtUtc;
}

