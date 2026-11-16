namespace GPMH.Domain.Enums;

/// <summary>
/// Approval lifecycle state for Field Agents (District Franchise Partners) under MOD-AGT (ADR-012).
/// </summary>
public enum AgentKycStatus
{
    PendingApproval = 1,
    Approved = 2,
    Suspended = 3,
    Rejected = 4,
}

