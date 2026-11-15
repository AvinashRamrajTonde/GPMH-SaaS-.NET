using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record GetDistrictsQuery : IRequest<IReadOnlyList<LgdDistrictDto>>;

public sealed class GetDistrictsQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<GetDistrictsQuery, IReadOnlyList<LgdDistrictDto>>
{
    public Task<IReadOnlyList<LgdDistrictDto>> Handle(GetDistrictsQuery request, CancellationToken cancellationToken) =>
        lgdClient.GetDistrictsAsync(cancellationToken);
}

