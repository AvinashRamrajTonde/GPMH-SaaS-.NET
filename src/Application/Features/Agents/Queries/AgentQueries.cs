using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.Features.Agents.Commands;
using GPMH.Domain.Entities.Agents;
using MediatR;

namespace GPMH.Application.Features.Agents.Queries;

public sealed record GetCurrentAgentProfileQuery : IRequest<AgentProfileDto>;

public sealed record GetSuperAdminAgentsQuery : IRequest<IReadOnlyList<AgentProfileDto>>;

public sealed class GetCurrentAgentProfileQueryHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetCurrentAgentProfileQuery, AgentProfileDto>
{
    public async Task<AgentProfileDto> Handle(GetCurrentAgentProfileQuery request, CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        return agent.ToProfileDto();
    }
}

public sealed class GetSuperAdminAgentsQueryHandler(IAgentRepository agentRepository)
    : IRequestHandler<GetSuperAdminAgentsQuery, IReadOnlyList<AgentProfileDto>>
{
    public async Task<IReadOnlyList<AgentProfileDto>> Handle(
        GetSuperAdminAgentsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldAgent> agents = await agentRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return agents.Select(a => a.ToProfileDto()).ToList();
    }
}

