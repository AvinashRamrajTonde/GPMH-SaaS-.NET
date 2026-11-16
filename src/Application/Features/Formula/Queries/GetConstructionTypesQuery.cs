using GPMH.Application.DTOs.Formula;
using MediatR;

namespace GPMH.Application.Features.Formula.Queries;

public sealed record GetConstructionTypesQuery : IRequest<IReadOnlyList<ConstructionTypeMasterDto>>;

public sealed class GetConstructionTypesQueryHandler(ISender sender)
    : IRequestHandler<GetConstructionTypesQuery, IReadOnlyList<ConstructionTypeMasterDto>>
{
    public async Task<IReadOnlyList<ConstructionTypeMasterDto>> Handle(
        GetConstructionTypesQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto workspace = await sender
            .Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken)
            .ConfigureAwait(false);

        return workspace.ConstructionTypes;
    }
}

