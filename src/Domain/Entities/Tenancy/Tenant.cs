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
    public bool IsTrial { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public Guid? ReferredByAgentId { get; set; }
    public string? ReferralCodeUsed { get; set; }

    public bool IsLicenseOperational(DateTimeOffset utcNow) =>
        !IsDeleted &&
        (Status == TenantStatus.Active || Status == TenantStatus.GracePeriod) &&
        (!SubscriptionExpiresAtUtc.HasValue || SubscriptionExpiresAtUtc.Value >= utcNow);

    public string GetEffectiveStatus(DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            return "Deleted";
        }

        if (Status == TenantStatus.Active &&
            SubscriptionExpiresAtUtc.HasValue &&
            SubscriptionExpiresAtUtc.Value < utcNow)
        {
            return nameof(TenantStatus.Expired);
        }

        return Status.ToString();
    }

    public int? GetDaysUntilExpiry(DateTimeOffset utcNow) =>
        SubscriptionExpiresAtUtc.HasValue
            ? (int)Math.Floor((SubscriptionExpiresAtUtc.Value.UtcDateTime.Date - utcNow.UtcDateTime.Date).TotalDays)
            : null;

    public void Activate(DateTimeOffset activatedAtUtc)
    {
        Status = TenantStatus.Active;
        UpdatedAtUtc = activatedAtUtc;
        RaiseDomainEvent(new TenantOnboardedDomainEvent(Id, LgdCode, activatedAtUtc));
    }

    public void Suspend(DateTimeOffset suspendedAtUtc)
    {
        Status = TenantStatus.Suspended;
        UpdatedAtUtc = suspendedAtUtc;
    }

    public void SoftDelete(Guid deletedByUserId, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
        UpdatedAtUtc = deletedAtUtc;
    }

    public void Restore(DateTimeOffset restoredAtUtc)
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedByUserId = null;
        UpdatedAtUtc = restoredAtUtc;
    }
}

