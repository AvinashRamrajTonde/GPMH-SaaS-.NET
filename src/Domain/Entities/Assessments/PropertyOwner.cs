using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Joint or primary citizen owner link on a Namuna 8 property (total active share = 100%).
/// </summary>
public sealed class PropertyOwner : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid CitizenId { get; set; }
    public decimal OwnershipSharePct { get; set; } = 100.00m;
    public bool IsPrimaryOwner { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

