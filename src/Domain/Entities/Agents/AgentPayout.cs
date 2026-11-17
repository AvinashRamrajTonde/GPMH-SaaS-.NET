using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Agents;

/// <summary>
/// Commission withdrawal request or disbursed HQ settlement voucher (VCH-YYYY-NNNN) for a Field Agent.
/// </summary>
public sealed class AgentPayout : BaseEntity, IGlobalEntity
{
    public const string StatusRequested = "Requested";
    public const string StatusDisbursed = "Disbursed";
    public const string StatusRejected = "Rejected";

    public Guid AgentId { get; set; }
    public required string VoucherNo { get; set; }
    public long VoucherSequence { get; set; }
    public required string FinancialYear { get; set; }
    public decimal PayoutAmount { get; set; }
    public required string PaymentMode { get; set; }
    public required string UtrReference { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string Status { get; set; }
    public string? ReceiptAssetKey { get; set; }
    public string? Remarks { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTimeOffset? RequestedAtUtc { get; set; }
    public Guid? SettledByUserId { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
}

