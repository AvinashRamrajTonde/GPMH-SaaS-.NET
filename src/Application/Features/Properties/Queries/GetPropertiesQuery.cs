using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Properties.Queries;

public sealed record GetPropertiesQuery : IRequest<IReadOnlyList<PropertyDto>>;

public sealed record GetPropertyByIdQuery(Guid Id) : IRequest<PropertyDetailDto>;

public sealed record GetPropertyHubKpisQuery : IRequest<PropertyHubKpisDto>;

public sealed class GetPropertiesQueryHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetPropertiesQuery, IReadOnlyList<PropertyDto>>,
      IRequestHandler<GetPropertyByIdQuery, PropertyDetailDto>,
      IRequestHandler<GetPropertyHubKpisQuery, PropertyHubKpisDto>
{
    public async Task<IReadOnlyList<PropertyDto>> Handle(
        GetPropertiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        return await propertyRepository
            .ListEnrichedAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PropertyDetailDto> Handle(GetPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        return await propertyRepository
            .GetDetailByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Property), request.Id);
    }

    public async Task<PropertyHubKpisDto> Handle(GetPropertyHubKpisQuery request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        return await propertyRepository
            .GetHubKpisAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
