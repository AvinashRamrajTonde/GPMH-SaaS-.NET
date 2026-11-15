using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Identity;

public sealed class AuthIdentityRepository(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IAuthIdentityRepository
{
    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        List<TenantMembership> items = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. items.OrderByDescending(m => m.IsDefault).ThenBy(m => m.CreatedAtUtc)];
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeRefreshTokenFamilyAsync(
        Guid familyId,
        string reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        List<RefreshToken> activeTokens = await dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (RefreshToken token in activeTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
            token.RevokedReason = reason;
        }
    }

    public async Task RecordSecurityEventAsync(
        Guid? tenantId,
        string actorId,
        string action,
        string detailJson,
        CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(
            new AuditLog
            {
                TenantId = tenantId,
                EntityName = nameof(RefreshToken),
                EntityId = actorId,
                Action = action,
                ActorId = actorId,
                ActorRole = currentUserService.Role?.ToString(),
                CorrelationId = currentUserService.CorrelationId,
                IpAddress = currentUserService.IpAddress,
                UserAgent = currentUserService.UserAgent,
                NewValuesJson = detailJson,
                TimestampUtc = dateTimeProvider.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

