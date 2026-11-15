using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Auditing;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Auditing;

public sealed class AuditLogReader(AppDbContext dbContext) : IAuditLogReader
{
    public async Task<IReadOnlyList<AuditLog>> GetEntityTrailAsync(
        string entityName,
        string entityId,
        Guid? scopedTenantId,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> query = dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == entityName && l.EntityId == entityId);

        if (scopedTenantId.HasValue)
        {
            query = query.Where(l => l.TenantId == scopedTenantId.Value);
        }

        List<AuditLog> items = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. items.OrderBy(l => l.TimestampUtc).ThenBy(l => l.CreatedAtUtc)];
    }
}

