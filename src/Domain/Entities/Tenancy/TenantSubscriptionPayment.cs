using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Records direct SuperAdmin or online subscription license payments for a Gram Panchayat tenant.
/// </summary>
public sealed class TenantSubscriptionPayment : BaseEntity, IGlobalEntity
{
    public Guid TenantId { get; set; }
    public required string PlanName { get; set; }
    public required string PaymentMode { get; set; }
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public int DurationDays { get; set; }
    public string? PaymentReference { get; set; }
    public string? InvoiceNo { get; set; }
    public Guid? SubmissionId { get; set; }
    public DateTimeOffset PaymentDateUtc { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? Notes { get; set; }
}

