using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Maps a tenant to shared-schema or dedicated database shard (ADR-003 graduation path).
/// </summary>
public sealed class TenantDatabaseMapping : BaseEntity, IGlobalEntity
{
    public Guid TenantId { get; set; }
    public required string IsolationTier { get; set; } = "SHARED";
    public string SchemaName { get; set; } = "public";
    public string? ConnectionSecretName { get; set; }
    public bool IsActive { get; set; } = true;
}

