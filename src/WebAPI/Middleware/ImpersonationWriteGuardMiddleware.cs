namespace GPMH.WebAPI.Middleware;

/// <summary>
/// Enforces strict read-only operation when a request carries a short-lived Support Impersonation JWT.
/// Executed immediately after authentication so mutating requests return 403 IMPERSONATION_READ_ONLY reliably.
/// </summary>
public sealed class ImpersonationWriteGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated == true &&
            string.Equals(context.User.FindFirst("support_impersonation")?.Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            string method = context.Request.Method;
            bool isWriteMethod =
                HttpMethods.IsPost(method) ||
                HttpMethods.IsPut(method) ||
                HttpMethods.IsPatch(method) ||
                HttpMethods.IsDelete(method);

            PathString path = context.Request.Path;
            bool isAllowedControlPath =
                path.StartsWithSegments("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/api/v1/superadmin/impersonate/exit", StringComparison.OrdinalIgnoreCase);

            if (isWriteMethod && !isAllowedControlPath)
            {
                await ProblemResponseHelper.WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Read-Only Support Impersonation",
                    "Write, delete, and financial operations are blocked during a support impersonation session.",
                    "IMPERSONATION_READ_ONLY").ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}

