using GPMH.Domain.Common;
using GPMH.Domain.Enums;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Verified membership mapping an authenticated user to a GP tenant and RBAC role.
/// Used by TenantResolutionMiddleware to validate X-Tenant-ID headers.
/// </summary>
public sealed class TenantMembership : BaseEntity, IGlobalEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public ApplicationRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

