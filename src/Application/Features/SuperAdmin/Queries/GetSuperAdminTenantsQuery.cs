using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using MediatR;

namespace GPMH.Application.Features.SuperAdmin.Queries;

public sealed record GetSuperAdminTenantsQuery : IRequest<IReadOnlyList<SuperAdminTenantSummaryDto>>;

public sealed class GetSuperAdminTenantsQueryHandler(ISuperAdminProvisioningRepository repository)
    : IRequestHandler<GetSuperAdminTenantsQuery, IReadOnlyList<SuperAdminTenantSummaryDto>>
{
    public Task<IReadOnlyList<SuperAdminTenantSummaryDto>> Handle(
        GetSuperAdminTenantsQuery request,
        CancellationToken cancellationToken) =>
        repository.ListTenantsAsync(cancellationToken);
}

