using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Tenancy;

public sealed class TenantMembershipValidator(AppDbContext dbContext)
    : ITenantMembershipValidator
{
    public Task<bool> IsActiveMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.TenantMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.IsActive,
                cancellationToken);
}

