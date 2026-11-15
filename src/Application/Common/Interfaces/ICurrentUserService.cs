using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Provides authenticated actor identity and request trace metadata for auditing and authorization.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    ApplicationRole? Role { get; }
    Guid? AgentId { get; }
    string? CorrelationId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsAuthenticated { get; }
}
