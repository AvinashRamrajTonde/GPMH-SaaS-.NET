using System.Security.Claims;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Enums;

namespace GPMH.WebAPI.Services;

public sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            string? raw = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(raw, out Guid id) ? id : null;
        }
    }

    public ApplicationRole? Role
    {
        get
        {
            string? raw = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("role");

            return Enum.TryParse<ApplicationRole>(raw, ignoreCase: true, out ApplicationRole role)
                ? role
                : null;
        }
    }

    public Guid? AgentId
    {
        get
        {
            string? raw = httpContextAccessor.HttpContext?.User.FindFirstValue("agent_id");
            return Guid.TryParse(raw, out Guid id) ? id : null;
        }
    }

    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items["CorrelationId"] as string
        ?? httpContextAccessor.HttpContext?.TraceIdentifier;

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true && UserId.HasValue;
}

