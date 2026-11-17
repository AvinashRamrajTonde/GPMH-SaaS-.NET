using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Self-service GP subscription renewal payment slip/UTR submission awaiting SuperAdmin review.
/// </summary>
public sealed class TenantPaymentSubmission : BaseEntity, IGlobalEntity
{
    public const string StatusPending = "Pending";
    public const string StatusApproved = "Approved";
    public const string StatusRejected = "Rejected";

    public Guid TenantId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string PlanName { get; set; } = "Standard";
    public string PaymentMode { get; set; } = "UPI";
    public decimal Amount { get; set; }
    public required string UtrReference { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? SlipAssetKey { get; set; }
    public string Status { get; set; } = StatusPending;
    public string? RejectionReason { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? ApprovedSubscriptionPaymentId { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Notes { get; set; }
}

