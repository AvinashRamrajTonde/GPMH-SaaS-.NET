using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Exceptions;

public sealed class AgentStatusGatedException : Exception
{
    public AgentStatusGatedException()
        : base("Field agent account is not approved for workspace access.")
    {
    }

    public AgentStatusGatedException(string message)
        : base(message)
    {
    }

    public AgentStatusGatedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AgentStatusGatedException(
        string agentCode,
        AgentKycStatus kycStatus,
        string? reason,
        string message)
        : base(message)
    {
        AgentCode = agentCode;
        KycStatus = kycStatus;
        Reason = reason;
    }

    public string AgentCode { get; } = string.Empty;
    public AgentKycStatus KycStatus { get; } = AgentKycStatus.PendingApproval;
    public string? Reason { get; }
    public string HelplinePhone { get; } = "0721-2550101";
    public string HelplineEmail { get; } = "support@epanchayat-mh.gov.in";
}

