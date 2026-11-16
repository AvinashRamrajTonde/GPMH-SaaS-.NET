using FluentValidation;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Queries;

/// <summary>Format values: A = general tax, B = water tax, C = combined (default).</summary>
public sealed record GetNamuna9RegisterQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    string? Format = "C",
    bool PendingOnly = false) : IRequest<Namuna9WorkspaceDto>;

public sealed class GetNamuna9RegisterQueryValidator : AbstractValidator<GetNamuna9RegisterQuery>
{
    public GetNamuna9RegisterQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(Namuna9Support.IsValidFormat)
            .WithMessage("Format must be A (general tax), B (water tax) or C (combined).");
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.WardNo).MaximumLength(40);
    }
}

public sealed class GetNamuna9RegisterQueryHandler(INamuna9Repository repository)
    : IRequestHandler<GetNamuna9RegisterQuery, Namuna9WorkspaceDto>
{
    public Task<Namuna9WorkspaceDto> Handle(GetNamuna9RegisterQuery request, CancellationToken cancellationToken) =>
        Namuna9Support.BuildWorkspaceAsync(
            repository,
            request.AssessmentYearId,
            request.WardNo,
            request.Search,
            request.Format,
            request.PendingOnly,
            cancellationToken);
}

