using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Identity;
using MediatR;

namespace GPMH.Application.Features.Auth.Commands;

public sealed record LogoutCommand(string? RawRefreshToken) : IRequest;

public sealed class LogoutCommandHandler(
    IAuthIdentityRepository authRepository,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return;
        }

        string tokenHash = jwtTokenService.ComputeTokenHash(request.RawRefreshToken.Trim());
        RefreshToken? existing = await authRepository
            .FindRefreshTokenByHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        await authRepository
            .RevokeRefreshTokenFamilyAsync(existing.FamilyId, "LOGOUT", dateTimeProvider.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        await authRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

