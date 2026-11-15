using GPMH.Domain.Common;
using GPMH.Domain.Enums;
using GPMH.Domain.Events;

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

    public void Activate(DateTimeOffset activatedAtUtc)
    {
        Status = TenantStatus.Active;
        UpdatedAtUtc = activatedAtUtc;
        RaiseDomainEvent(new TenantOnboardedDomainEvent(Id, LgdCode, activatedAtUtc));
    }
}

