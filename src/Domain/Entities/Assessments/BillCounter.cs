using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Atomic per-tenant, per-financial-year sequential counter backing gap-free Namuna 9-Ka bill numbering.
/// </summary>
public sealed class BillCounter : ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string FinancialYear { get; set; }
    public long LastSequence { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

