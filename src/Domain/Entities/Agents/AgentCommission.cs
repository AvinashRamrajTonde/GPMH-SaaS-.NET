using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Agents;

/// <summary>
/// Immutable rate-snapshotted commission earned by a Field Agent on GP onboarding or renewal (ADR-013).
/// </summary>
public sealed class AgentCommission : BaseEntity, IGlobalEntity
{
    public const string TypeNewOnboarding = "NEW_ONBOARDING";
    public const string TypeRenewal = "RENEWAL";
    public const string TypeClawback = "CLAWBACK";

    public const string StatusAccrued = "Accrued";
    public const string StatusRequested = "Requested";
    public const string StatusPartiallySettled = "PartiallySettled";
    public const string StatusSettled = "Settled";
    public const string StatusClawedBack = "ClawedBack";

    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SubscriptionPaymentId { get; set; }
    public required string CommissionType { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RatePctSnapshot { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public required string Status { get; set; }
    public Guid? LatestPayoutId { get; set; }
    public string? Notes { get; set; }

    public decimal GetRemainingPayable()
    {
        if (string.Equals(CommissionType, TypeClawback, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, StatusClawedBack, StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return Math.Max(0m, CommissionAmount - SettledAmount);
    }
}

