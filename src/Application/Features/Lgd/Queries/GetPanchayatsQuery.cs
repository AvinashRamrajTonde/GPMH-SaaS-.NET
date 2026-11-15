using FluentValidation;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record GetPanchayatsQuery(string District, string Tehsil)
    : IRequest<IReadOnlyList<LgdPanchayatSummaryDto>>;

public sealed class GetPanchayatsQueryValidator : AbstractValidator<GetPanchayatsQuery>
{
    public GetPanchayatsQueryValidator()
    {
        RuleFor(x => x.District).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Tehsil).NotEmpty().MaximumLength(120);
    }
}

public sealed class GetPanchayatsQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<GetPanchayatsQuery, IReadOnlyList<LgdPanchayatSummaryDto>>
{
    public Task<IReadOnlyList<LgdPanchayatSummaryDto>> Handle(
        GetPanchayatsQuery request,
        CancellationToken cancellationToken) =>
        lgdClient.GetPanchayatsAsync(request.District, request.Tehsil, cancellationToken);
}

