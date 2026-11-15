using GPMH.Domain.Common;
using GPMH.Domain.Enums;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Platform tenant representing one subscribed Maharashtra Gram Panchayat.
/// </summary>
public sealed class Tenant : BaseEntity, IGlobalEntity
{
    public required string TenantCode { get; set; }
    public required string LgdCode { get; set; }
    public required string NameEn { get; set; }
    public required string NameMr { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.PendingApproval;
    public DateTimeOffset? SubscriptionExpiresAtUtc { get; set; }
}

