using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using MediatR;

namespace GPMH.Application.Features.SuperAdmin.Queries;

public sealed record GetSuperAdminTenantsQuery(
    string? Search = null,
    string? District = null,
    string? Taluka = null,
    string? Status = null,
    bool IncludeDeleted = false) : IRequest<IReadOnlyList<SuperAdminTenantSummaryDto>>;

public sealed class GetSuperAdminTenantsQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminTenantsQuery, IReadOnlyList<SuperAdminTenantSummaryDto>>
{
    public Task<IReadOnlyList<SuperAdminTenantSummaryDto>> Handle(
        GetSuperAdminTenantsQuery request,
        CancellationToken cancellationToken) =>
        repository.ListTenantsFilteredAsync(
            request.Search,
            request.District,
            request.Taluka,
            request.Status,
            request.IncludeDeleted,
            cancellationToken);
}

