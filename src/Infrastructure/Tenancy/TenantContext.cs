using GPMH.Application.Common.Interfaces;

namespace GPMH.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? CurrentTenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        CurrentTenantId = tenantId;
    }

    public void Clear() => CurrentTenantId = null;
}

