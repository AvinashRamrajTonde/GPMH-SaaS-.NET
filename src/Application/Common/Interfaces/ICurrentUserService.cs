using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Provides authenticated actor identity and correlation metadata for auditing and authorization.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    ApplicationRole? Role { get; }
    Guid? AgentId { get; }
    string? CorrelationId { get; }
    bool IsAuthenticated { get; }
}

