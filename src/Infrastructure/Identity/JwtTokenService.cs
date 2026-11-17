using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GPMH.Infrastructure.Identity;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider)
    : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        User user,
        ApplicationRole role,
        Guid? tenantId,
        Guid? agentId = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes for HS256 signing.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateTimeOffset expiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes > 0 ? _options.AccessTokenMinutes : 15);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(ClaimTypes.Role, role.ToString()),
        ];

        if (tenantId is Guid resolvedTenant && resolvedTenant != Guid.Empty)
        {
            claims.Add(new Claim("tenant_id", resolvedTenant.ToString("D")));
        }

        if (agentId is Guid resolvedAgent && resolvedAgent != Guid.Empty)
        {
            claims.Add(new Claim("agent_id", resolvedAgent.ToString("D")));
        }

        return SignJwtClaims(claims, now, expiresAtUtc);
    }

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateImpersonationToken(
        User impersonator,
        Tenant targetTenant,
        ApplicationRole impersonatorRole,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(impersonator);
        ArgumentNullException.ThrowIfNull(targetTenant);

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes for HS256 signing.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateTimeOffset expiresAtUtc = now.Add(duration > TimeSpan.Zero ? duration : TimeSpan.FromMinutes(15));

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, impersonator.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, impersonator.Id.ToString("D")),
            new(JwtRegisteredClaimNames.UniqueName, impersonator.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(ClaimTypes.Role, nameof(ApplicationRole.Auditor)),
            new("support_impersonation", "true"),
            new("impersonator_id", impersonator.Id.ToString("D")),
            new("impersonator_role", impersonatorRole.ToString()),
            new("tenant_id", targetTenant.Id.ToString("D")),
            new("impersonation_expires_utc", expiresAtUtc.ToString("O")),
            new("impersonated_tenant_name", targetTenant.NameEn),
        ];

        return SignJwtClaims(claims, now, expiresAtUtc);
    }

    private (string AccessToken, DateTimeOffset ExpiresAtUtc) SignJwtClaims(
        IEnumerable<Claim> claims,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return (serialized, expiresAtUtc);
    }

    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        string rawToken = Base64Url.EncodeToString(randomBytes);
        return (rawToken, ComputeTokenHash(rawToken));
    }

    public string ComputeTokenHash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(digest);
    }
}

