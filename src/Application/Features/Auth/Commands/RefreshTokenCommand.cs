using System.Text.Json;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(
    string RawRefreshToken,
    Guid? RequestedTenantId = null) : IRequest<AuthTokenBundleDto>;

public sealed class RefreshTokenCommandHandler(
    IAuthIdentityRepository authRepository,
    IAgentRepository agentRepository,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RefreshTokenCommand, AuthTokenBundleDto>
{
    public async Task<AuthTokenBundleDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            throw new AuthenticationFailedException("Refresh token is missing.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        string incomingHash = jwtTokenService.ComputeTokenHash(request.RawRefreshToken.Trim());

        RefreshToken? existing = await authRepository
            .FindRefreshTokenByHashAsync(incomingHash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        // Reuse detection (ADR-006): presenting an already-rotated or revoked token compromises the whole family.
        if (existing.RevokedAtUtc.HasValue)
        {
            await authRepository
                .RevokeRefreshTokenFamilyAsync(existing.FamilyId, "REUSE_DETECTED", now, cancellationToken)
                .ConfigureAwait(false);

            await authRepository
                .RecordSecurityEventAsync(
                    tenantId: null,
                    actorId: existing.UserId.ToString("D"),
                    action: "RefreshTokenReuseDetected",
                    detailJson: JsonSerializer.Serialize(new
                    {
                        familyId = existing.FamilyId,
                        reusedTokenId = existing.Id,
                        originalRevokedReason = existing.RevokedReason,
                    }),
                    cancellationToken)
                .ConfigureAwait(false);

            await authRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            throw new AuthenticationFailedException("Refresh token reuse detected. Session family has been revoked.");
        }

        if (existing.ExpiresAtUtc <= now)
        {
            existing.RevokedAtUtc = now;
            existing.RevokedReason = "EXPIRED";
            await authRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new AuthenticationFailedException("Refresh token expired.");
        }

        User? user = await authRepository
            .FindUserByIdAsync(existing.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException("User account is inactive.");
        }

        IReadOnlyList<TenantMembership> memberships = await authRepository
            .GetActiveMembershipsAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        (ApplicationRole role, Guid? activeTenantId, Guid? agentId) = await LoginCommandHandler
            .ResolveActorScopeAsync(user, memberships, request.RequestedTenantId, agentRepository, cancellationToken)
            .ConfigureAwait(false);

        (string accessToken, DateTimeOffset expiresAtUtc) = jwtTokenService.CreateAccessToken(user, role, activeTenantId, agentId);
        (string newRawToken, string newHash) = jwtTokenService.CreateRefreshToken();

        RefreshToken replacement = new()
        {
            UserId = user.Id,
            TokenHash = newHash,
            FamilyId = existing.FamilyId,
            ExpiresAtUtc = now.AddDays(7),
        };

        existing.RevokedAtUtc = now;
        existing.ReplacedByTokenId = replacement.Id;
        existing.RevokedReason = "ROTATED";

        await authRepository.AddRefreshTokenAsync(replacement, cancellationToken).ConfigureAwait(false);
        await authRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        List<TenantMembershipSummaryDto> membershipSummaries = memberships
            .Select(m => new TenantMembershipSummaryDto(m.TenantId, m.Role, m.IsDefault))
            .ToList();

        AuthenticatedUserDto userDto = new(
            user.Id,
            user.Username,
            user.FullNameEn,
            user.FullNameMr,
            role,
            activeTenantId,
            membershipSummaries);

        return new AuthTokenBundleDto(accessToken, newRawToken, expiresAtUtc, userDto);
    }
}

