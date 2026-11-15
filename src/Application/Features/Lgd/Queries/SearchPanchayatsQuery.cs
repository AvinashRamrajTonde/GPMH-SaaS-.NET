using FluentValidation;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record SearchPanchayatsQuery(string Query, int Limit = 20)
    : IRequest<IReadOnlyList<LgdPanchayatSummaryDto>>;

public sealed class SearchPanchayatsQueryValidator : AbstractValidator<SearchPanchayatsQuery>
{
    public SearchPanchayatsQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MinimumLength(2).WithMessage("Search query must be at least 2 characters.")
            .MaximumLength(120);

        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

public sealed class SearchPanchayatsQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<SearchPanchayatsQuery, IReadOnlyList<LgdPanchayatSummaryDto>>
{
    public Task<IReadOnlyList<LgdPanchayatSummaryDto>> Handle(
        SearchPanchayatsQuery request,
        CancellationToken cancellationToken) =>
        lgdClient.SearchPanchayatsAsync(request.Query, request.Limit, cancellationToken);
}

