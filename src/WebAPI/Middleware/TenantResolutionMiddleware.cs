using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Enums;
using Serilog.Context;

namespace GPMH.WebAPI.Middleware;

/// <summary>
/// Resolves X-Tenant-ID strictly as a selector, never an authorization credential.
/// Verifies active TenantMembership before activating scoped ITenantContext.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-ID";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ITenantMembershipValidator membershipValidator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(currentUserService);
        ArgumentNullException.ThrowIfNull(membershipValidator);

        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            if (!Guid.TryParse(headerValue.ToString().Trim(), out Guid requestedTenantId) || requestedTenantId == Guid.Empty)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [HeaderName] = ["X-Tenant-ID header must be a valid non-empty UUID."],
                });
            }

            if (!currentUserService.IsAuthenticated || currentUserService.UserId is not Guid userId)
            {
                throw new TenantAccessDeniedException("Authenticated user context is required for tenant selection.");
            }

            if (currentUserService.Role != ApplicationRole.SuperAdmin)
            {
                bool isMember = await membershipValidator
                    .IsActiveMemberAsync(userId, requestedTenantId, context.RequestAborted)
                    .ConfigureAwait(false);

                if (!isMember)
                {
                    throw new TenantAccessDeniedException("User does not hold an active membership in the requested tenant.");
                }
            }

            tenantContext.SetTenant(requestedTenantId);
        }
        else if (currentUserService.IsAuthenticated
            && Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out Guid claimTenantId)
            && claimTenantId != Guid.Empty)
        {
            bool isMember = currentUserService.Role == ApplicationRole.SuperAdmin ||
                await membershipValidator
                    .IsActiveMemberAsync(currentUserService.UserId!.Value, claimTenantId, context.RequestAborted)
                    .ConfigureAwait(false);

            if (!isMember)
            {
                throw new TenantAccessDeniedException("Token tenant claim is not backed by an active membership.");
            }

            tenantContext.SetTenant(claimTenantId);
        }

        using (LogContext.PushProperty("TenantId", tenantContext.CurrentTenantId?.ToString() ?? "none"))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}

