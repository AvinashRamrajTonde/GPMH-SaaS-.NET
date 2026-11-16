using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9Ka;
using MediatR;

namespace GPMH.Application.Features.Namuna9Ka.Queries;

public sealed record GetNamuna9KaBillsQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    bool PendingOnly = false,
    IReadOnlyCollection<Guid>? PropertyIds = null) : IRequest<Namuna9KaWorkspaceDto>;

public sealed class GetNamuna9KaBillsQueryValidator : AbstractValidator<GetNamuna9KaBillsQuery>
{
    public GetNamuna9KaBillsQueryValidator()
    {
        RuleFor(x => x.WardNo).MaximumLength(40);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class GetNamuna9KaBillsQueryHandler(
    INamuna9KaRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetNamuna9KaBillsQuery, Namuna9KaWorkspaceDto>
{
    public Task<Namuna9KaWorkspaceDto> Handle(GetNamuna9KaBillsQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        return repository.LoadWorkspaceAsync(
            tenantId,
            request.AssessmentYearId,
            request.WardNo,
            request.Search,
            request.PendingOnly,
            request.PropertyIds,
            cancellationToken);
    }
}

