using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Agents;

/// <summary>
/// Global atomic per-financial-year sequential counter backing gap-free agent payout voucher numbering (VCH-YYYY-NNNN).
/// </summary>
public sealed class VoucherCounter : IGlobalEntity
{
    public static readonly Guid GlobalTenantId = Guid.Empty;

    public Guid TenantId { get; set; } = GlobalTenantId;
    public required string FinancialYear { get; set; }
    public long LastSequence { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

