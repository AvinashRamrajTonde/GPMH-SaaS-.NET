using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Tenant-scoped financial assessment year (e.g. "2026-27") with period locking for FY rollover.
/// </summary>
public sealed class AssessmentYear : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string YearLabel { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
}

