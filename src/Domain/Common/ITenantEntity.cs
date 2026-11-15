namespace GPMH.Domain.Common;

/// <summary>
/// Marker interface for multi-tenant entities owned by a specific Gram Panchayat tenant.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

