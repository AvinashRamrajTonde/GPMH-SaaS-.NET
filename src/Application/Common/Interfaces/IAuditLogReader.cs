using GPMH.Domain.Entities.Auditing;

namespace GPMH.Application.Common.Interfaces;

public interface IAuditLogReader
{
    Task<IReadOnlyList<AuditLog>> GetEntityTrailAsync(
        string entityName,
        string entityId,
        Guid? scopedTenantId,
        CancellationToken cancellationToken);
}

