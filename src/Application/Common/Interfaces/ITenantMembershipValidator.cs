namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Verifies whether an authenticated user holds an active membership in the requested GP tenant.
/// </summary>
public interface ITenantMembershipValidator
{
    Task<bool> IsActiveMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}

