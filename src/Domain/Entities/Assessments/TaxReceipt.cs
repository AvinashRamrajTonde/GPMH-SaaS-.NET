using GPMH.Domain.Common;
using GPMH.Domain.Enums;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Immutable statutory Namuna 10 property tax collection receipt (नमुना १० कर वसुली पावती).
/// Corrections require an audited Gramsevak cancellation reversal rather than in-place editing (ADR-008).
/// </summary>
public sealed class TaxReceipt : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid PropertyDemandId { get; set; }
    public Guid AssessmentYearId { get; set; }
    public Guid OperationId { get; set; }
    public string BookNo { get; set; } = "BOOK-1";
    public required string ReceiptNo { get; set; }
    public long ReceiptSequence { get; set; }
    public DateOnly ReceiptDate { get; set; }
    public DateTimeOffset CollectedAtUtc { get; set; }
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    public required string PayerName { get; set; }
    public string? ReferenceNo { get; set; }
    public string? BankName { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public string? Remarks { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal DiscountGiven { get; set; }
    public decimal ExcessCredit { get; set; }
    public string HeadAllocationJson { get; set; } = "{}";
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid CollectedByUserId { get; set; }
    public Guid? DemandBillId { get; set; }
}

