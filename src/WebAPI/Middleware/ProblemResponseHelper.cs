using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Middleware;

internal static class ProblemResponseHelper
{
    public static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        string correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
        string traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            Instance = context.Request.Path,
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = traceId;

        if (extensions is not null)
        {
            foreach (KeyValuePair<string, object?> kv in extensions)
            {
                problem.Extensions[kv.Key] = kv.Value;
            }
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }
}

