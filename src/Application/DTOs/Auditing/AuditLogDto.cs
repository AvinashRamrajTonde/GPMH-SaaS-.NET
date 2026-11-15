namespace GPMH.Application.DTOs.Auditing;

public sealed record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    string EntityName,
    string EntityId,
    string Action,
    string? ActorId,
    string? ActorRole,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    string? OldValuesJson,
    string? NewValuesJson,
    string? ChangedColumnsJson,
    DateTimeOffset TimestampUtc);

