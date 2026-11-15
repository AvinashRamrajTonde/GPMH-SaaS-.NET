namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Scoped ambient Gram Panchayat tenant context populated after server-side membership verification.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
    bool IsResolved => CurrentTenantId.HasValue;
    void SetTenant(Guid tenantId);
    void Clear();
}

