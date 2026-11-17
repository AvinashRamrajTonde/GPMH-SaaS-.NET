using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Auth.Commands;

public sealed record LoginCommand(
    string Username,
    string Password,
    Guid? TenantId = null) : IRequest<AuthTokenBundleDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class LoginCommandHandler(
    IAuthIdentityRepository authRepository,
    IAgentRepository agentRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LoginCommand, AuthTokenBundleDto>
{
    public async Task<AuthTokenBundleDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        string normalizedUsername = request.Username.Trim();
        User? user = await authRepository
            .FindUserByUsernameAsync(normalizedUsername, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        IReadOnlyList<TenantMembership> memberships = await authRepository
            .GetActiveMembershipsAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        (ApplicationRole role, Guid? activeTenantId, Guid? agentId) = await ResolveActorScopeAsync(
            user,
            memberships,
            request.TenantId,
            agentRepository,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        (string accessToken, DateTimeOffset expiresAtUtc) = jwtTokenService.CreateAccessToken(user, role, activeTenantId, agentId);
        (string rawRefreshToken, string refreshTokenHash) = jwtTokenService.CreateRefreshToken();

        RefreshToken refreshEntity = new()
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = now.AddDays(7),
        };

        await authRepository.AddRefreshTokenAsync(refreshEntity, cancellationToken).ConfigureAwait(false);
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

        return new AuthTokenBundleDto(accessToken, rawRefreshToken, expiresAtUtc, userDto);
    }

    internal static async Task<(ApplicationRole Role, Guid? TenantId, Guid? AgentId)> ResolveActorScopeAsync(
        User user,
        IReadOnlyList<TenantMembership> memberships,
        Guid? requestedTenantId,
        IAgentRepository agentRepository,
        CancellationToken cancellationToken)
    {
        if (user.IsSuperAdmin)
        {
            return (ApplicationRole.SuperAdmin, requestedTenantId, null);
        }

        if (user.IsSupportAdmin)
        {
            return (ApplicationRole.Support_Admin, requestedTenantId, null);
        }

        if (memberships.Count == 0)
        {
            FieldAgent? agent = await agentRepository
                .FindByUserIdAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            if (agent is null)
            {
                throw new AuthenticationFailedException("User has no active Gram Panchayat membership.");
            }

            if (agent.KycStatus != AgentKycStatus.Approved)
            {
                throw new AgentStatusGatedException(
                    agent.AgentCode,
                    agent.KycStatus,
                    agent.StatusReason,
                    $"Agent account '{agent.AgentCode}' is '{agent.KycStatus}' and awaiting approval.");
            }

            return (ApplicationRole.Field_Agent, null, agent.Id);
        }

        TenantMembership? selected;
        if (requestedTenantId.HasValue)
        {
            selected = memberships.FirstOrDefault(m => m.TenantId == requestedTenantId.Value)
                ?? throw new TenantAccessDeniedException("User does not hold an active membership in the requested tenant.");
        }
        else
        {
            selected = memberships.FirstOrDefault(m => m.IsDefault)
                ?? memberships[0];
        }

        return (selected.Role, selected.TenantId, null);
    }
}

