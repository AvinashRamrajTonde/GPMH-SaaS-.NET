namespace GPMH.Domain.Enums;

/// <summary>
/// Subscription and operational lifecycle state for a Gram Panchayat tenant.
/// </summary>
public enum TenantStatus
{
    PendingApproval = 1,
    Active = 2,
    GracePeriod = 3,
    Suspended = 4,
    Expired = 5,
}

