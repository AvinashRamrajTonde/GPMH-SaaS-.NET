using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Auth.Queries;

public sealed record GetCurrentUserQuery : IRequest<AuthenticatedUserDto>;

public sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IAuthIdentityRepository authRepository)
    : IRequestHandler<GetCurrentUserQuery, AuthenticatedUserDto>
{
    public async Task<AuthenticatedUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is not Guid userId)
        {
            throw new AuthenticationFailedException("Authenticated user context is required.");
        }

        User? user = await authRepository
            .FindUserByIdAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(User), userId);

        IReadOnlyList<TenantMembership> memberships = await authRepository
            .GetActiveMembershipsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        ApplicationRole activeRole = currentUserService.Role
            ?? (user.IsSuperAdmin ? ApplicationRole.SuperAdmin : memberships.FirstOrDefault()?.Role ?? ApplicationRole.Citizen);

        List<TenantMembershipSummaryDto> membershipSummaries = memberships
            .Select(m => new TenantMembershipSummaryDto(m.TenantId, m.Role, m.IsDefault))
            .ToList();

        return new AuthenticatedUserDto(
            user.Id,
            user.Username,
            user.FullNameEn,
            user.FullNameMr,
            activeRole,
            tenantContext.CurrentTenantId,
            membershipSummaries);
    }
}

