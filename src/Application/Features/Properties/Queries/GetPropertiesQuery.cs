using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Application.Features.Properties.Commands;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Properties.Queries;

public sealed record GetPropertiesQuery : IRequest<IReadOnlyList<PropertyDto>>;

public sealed record GetPropertyByIdQuery(Guid Id) : IRequest<PropertyDto>;

public sealed class GetPropertiesQueryHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetPropertiesQuery, IReadOnlyList<PropertyDto>>
{
    public async Task<IReadOnlyList<PropertyDto>> Handle(
        GetPropertiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        IReadOnlyList<Property> properties = await propertyRepository
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        return properties.Select(p => p.ToDto()).ToList();
    }
}

public sealed class GetPropertyByIdQueryHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetPropertyByIdQuery, PropertyDto>
{
    public async Task<PropertyDto> Handle(GetPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        Property property = await propertyRepository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Property), request.Id);

        return property.ToDto();
    }
}

