using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Enums;

namespace GPMH.WebAPI.Middleware;

/// <summary>
/// Enforces tenant subscription lifecycle on mutating ERP endpoints while keeping auth, onboarding,
/// payment renewal submission, asset uploads, and SuperAdmin routes accessible.
/// </summary>
public sealed class TenantLicenseMiddleware(RequestDelegate next)
{
    private static readonly PathString[] ExemptPrefixes =
    [
        "/api/v1/auth",
        "/api/v1/onboarding",
        "/api/v1/assets",
        "/api/v1/dashboard",
        "/api/v1/superadmin",
        "/api/v1/lgd",
        "/api/v1/public",
        "/health",
        "/swagger",
        "/scalar",
        "/openapi",
    ];

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantLicenseChecker licenseChecker,
        IDateTimeProvider dateTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(licenseChecker);
        ArgumentNullException.ThrowIfNull(dateTimeProvider);

        string method = context.Request.Method;
        bool isWrite =
            HttpMethods.IsPost(method) ||
            HttpMethods.IsPut(method) ||
            HttpMethods.IsPatch(method) ||
            HttpMethods.IsDelete(method);

        if (isWrite &&
            context.User.Identity?.IsAuthenticated == true &&
            tenantContext.CurrentTenantId is Guid tenantId &&
            !IsExemptPath(context.Request.Path))
        {
            TenantLicenseState? state = await licenseChecker
                .GetLicenseStateAsync(tenantId, context.RequestAborted)
                .ConfigureAwait(false);

            if (state is not null)
            {
                DateTimeOffset now = dateTimeProvider.UtcNow;
                bool expired = state.Status == TenantStatus.Expired ||
                    (state.Status == TenantStatus.Active &&
                     state.SubscriptionExpiresAtUtc.HasValue &&
                     state.SubscriptionExpiresAtUtc.Value < now);

                bool blocked = state.IsDeleted ||
                    state.Status is TenantStatus.PendingApproval or TenantStatus.Suspended ||
                    expired;

                if (blocked)
                {
                    int statusCode = (state.IsDeleted || state.Status == TenantStatus.Suspended)
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status402PaymentRequired;

                    string effective = state.IsDeleted
                        ? "Deleted"
                        : expired
                            ? nameof(TenantStatus.Expired)
                            : state.Status.ToString();

                    await ProblemResponseHelper.WriteProblemAsync(
                        context,
                        statusCode,
                        "Gram Panchayat License Inactive",
                        $"Operational writes are disabled while subscription status is '{effective}'. Submit payment renewal or contact Support.",
                        "TENANT_LICENSE_INACTIVE",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["subscriptionStatus"] = effective,
                            ["subscriptionExpiresAtUtc"] = state.SubscriptionExpiresAtUtc,
                        }).ConfigureAwait(false);
                    return;
                }
            }
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsExemptPath(PathString path)
    {
        foreach (PathString prefix in ExemptPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

