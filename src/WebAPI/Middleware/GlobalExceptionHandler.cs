using System.Diagnostics;
using GPMH.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        string correlationId = httpContext.Items["CorrelationId"] as string ?? httpContext.TraceIdentifier;
        string? traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problem = exception switch
        {
            DomainValidationException validationEx => new HttpValidationProblemDetails(
                new Dictionary<string, string[]>(validationEx.Errors, StringComparer.Ordinal))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = validationEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
            AuthenticationFailedException authEx => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication Failed",
                Detail = authEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
            TenantAccessDeniedException deniedEx => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tenant Access Denied",
                Detail = deniedEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
            AgentStatusGatedException agentGateEx => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Field Agent Status Gated",
                Detail = agentGateEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
                Extensions =
                {
                    ["agentCode"] = agentGateEx.AgentCode,
                    ["kycStatus"] = agentGateEx.KycStatus.ToString(),
                    ["reason"] = agentGateEx.Reason,
                    ["helplinePhone"] = agentGateEx.HelplinePhone,
                    ["helplineEmail"] = agentGateEx.HelplineEmail,
                },
            },
            NotFoundException notFoundEx => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = notFoundEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
            DomainConflictException conflictEx => CreateConflictProblem(conflictEx),
            ArgumentException argEx => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = argEx.Message,
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected server error occurred. Quote correlationId to support.",
                Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            },
        };

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = traceId;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception mapped to HTTP 500. CorrelationId={CorrelationId}", correlationId);
        }
        else
        {
            logger.LogWarning("Handled domain exception {ExceptionType} mapped to HTTP {Status}. CorrelationId={CorrelationId}",
                exception.GetType().Name, problem.Status, correlationId);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static ProblemDetails CreateConflictProblem(DomainConflictException ex)
    {
        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Domain State Conflict",
            Detail = ex.Message,
            Type = "https://tools.ietf.org/html/rfc9457#section-3.1",
            Extensions =
            {
                ["errorCode"] = ex.ErrorCode,
            },
        };

        foreach ((string key, object? value) in ex.Metadata)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }
}

