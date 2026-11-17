using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Interfaces;

public interface IPasswordHasherService
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string providedPassword);
}

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        User user,
        ApplicationRole role,
        Guid? tenantId,
        Guid? agentId = null);

    (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateImpersonationToken(
        User impersonator,
        Tenant targetTenant,
        ApplicationRole impersonatorRole,
        TimeSpan duration);

    (string RawToken, string TokenHash) CreateRefreshToken();

    string ComputeTokenHash(string rawToken);
}

public interface IAuthIdentityRepository
{
    Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantMembership>> GetActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task RevokeRefreshTokenFamilyAsync(
        Guid familyId,
        string reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);

    Task RecordSecurityEventAsync(
        Guid? tenantId,
        string actorId,
        string action,
        string detailJson,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

