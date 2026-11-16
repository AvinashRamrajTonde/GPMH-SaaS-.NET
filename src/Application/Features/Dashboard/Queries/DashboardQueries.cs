using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Dashboard;
using MediatR;

namespace GPMH.Application.Features.Dashboard.Queries;

public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed record SearchDashboardQuery(string Query, int Limit = 10) : IRequest<DashboardSearchResultDto>;

public sealed class SearchDashboardQueryValidator : AbstractValidator<SearchDashboardQuery>
{
    public SearchDashboardQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Limit).InclusiveBetween(1, 25);
    }
}

public sealed class GetDashboardSummaryQueryHandler(
    IDashboardReadRepository dashboardRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Valid tenant context is required.");

        return dashboardRepository.GetTenantSummaryAsync(tenantId, cancellationToken);
    }
}

public sealed class SearchDashboardQueryHandler(
    IDashboardReadRepository dashboardRepository,
    ITenantContext tenantContext)
    : IRequestHandler<SearchDashboardQuery, DashboardSearchResultDto>
{
    public Task<DashboardSearchResultDto> Handle(SearchDashboardQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Valid tenant context is required.");

        return dashboardRepository.SearchTenantRecordsAsync(tenantId, request.Query, request.Limit, cancellationToken);
    }
}

