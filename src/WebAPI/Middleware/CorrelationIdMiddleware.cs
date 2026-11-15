using Serilog.Context;

namespace GPMH.WebAPI.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues raw)
            && !string.IsNullOrWhiteSpace(raw)
                ? raw.ToString().Trim()
                : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}

