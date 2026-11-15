using FluentValidation;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record GetTehsilsQuery(string District) : IRequest<IReadOnlyList<LgdTehsilDto>>;

public sealed class GetTehsilsQueryValidator : AbstractValidator<GetTehsilsQuery>
{
    public GetTehsilsQueryValidator()
    {
        RuleFor(x => x.District)
            .NotEmpty().WithMessage("District is required.")
            .MaximumLength(120);
    }
}

public sealed class GetTehsilsQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<GetTehsilsQuery, IReadOnlyList<LgdTehsilDto>>
{
    public Task<IReadOnlyList<LgdTehsilDto>> Handle(GetTehsilsQuery request, CancellationToken cancellationToken) =>
        lgdClient.GetTehsilsAsync(request.District, cancellationToken);
}

