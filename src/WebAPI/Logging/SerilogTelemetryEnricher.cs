using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace GPMH.WebAPI.Logging;

/// <summary>
/// Enriches every Serilog event with active distributed TraceId, SpanId, and runtime Environment.
/// CorrelationId and TenantId are pushed per-request by middleware onto LogContext.
/// </summary>
public sealed class SerilogTelemetryEnricher(string environmentName) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Environment", environmentName));

        string traceId = Activity.Current?.TraceId.ToString() ?? "none";
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));

        if (Activity.Current is { } current)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", current.SpanId.ToString()));
        }
    }
}

