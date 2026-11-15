using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Auditing;

/// <summary>
/// Append-only audit log recording field-level JSON diffs in the same database transaction.
/// PostgreSQL privileges must REVOKE UPDATE and DELETE on audit_logs from gpmh_app.
/// </summary>
public sealed class AuditLog : BaseEntity, IGlobalEntity
{
    public Guid? TenantId { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public string? ActorId { get; set; }
    public string? ActorRole { get; set; }
    public string? CorrelationId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangedColumnsJson { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

