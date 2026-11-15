using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record GetLgdStatsQuery : IRequest<LgdStatsDto>;

public sealed class GetLgdStatsQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<GetLgdStatsQuery, LgdStatsDto>
{
    public async Task<LgdStatsDto> Handle(GetLgdStatsQuery request, CancellationToken cancellationToken) =>
        await lgdClient.GetStatsAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException("LgdStats", "global");
}

