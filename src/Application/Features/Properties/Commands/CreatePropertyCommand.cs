using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Properties.Commands;

public sealed record CreatePropertyCommand(
    string WardNo,
    string PropertyNo,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    string? BoundaryEast = null,
    string? BoundaryWest = null,
    string? BoundaryNorth = null,
    string? BoundarySouth = null,
    string WaterConnectionCode = "NONE",
    bool IsTaxExempt = false,
    Guid? ParentPropertyId = null) : IRequest<PropertyDto>;

public sealed class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyCommandValidator()
    {
        RuleFor(x => x.WardNo).NotEmpty().MaximumLength(40);
        RuleFor(x => x.PropertyNo).NotEmpty().MaximumLength(40);
        RuleFor(x => x.BuiltUpAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OpenPlotAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.BuiltUpAreaSqFt + x.OpenPlotAreaSqFt)
            .GreaterThan(0m)
            .WithMessage("Total property area must be greater than zero.");
        RuleFor(x => x.WaterConnectionCode).NotEmpty().MaximumLength(24);
    }
}

public sealed class CreatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<CreatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string wardNo = request.WardNo.Trim();
        string propertyNo = request.PropertyNo.Trim();

        if (await propertyRepository.ExistsByWardAndNoAsync(wardNo, propertyNo, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.PropertyNo)] = [$"Property '{wardNo}/{propertyNo}' already exists in this Gram Panchayat."],
            });
        }

        Property property = new()
        {
            TenantId = tenantId,
            ParentPropertyId = request.ParentPropertyId,
            WardNo = wardNo,
            PropertyNo = propertyNo,
            BuiltUpAreaSqFt = request.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt = request.OpenPlotAreaSqFt,
            TotalAreaSqFt = request.BuiltUpAreaSqFt + request.OpenPlotAreaSqFt,
            BoundaryEast = request.BoundaryEast?.Trim(),
            BoundaryWest = request.BoundaryWest?.Trim(),
            BoundaryNorth = request.BoundaryNorth?.Trim(),
            BoundarySouth = request.BoundarySouth?.Trim(),
            WaterConnectionCode = request.WaterConnectionCode.Trim().ToUpperInvariant(),
            IsTaxExempt = request.IsTaxExempt,
        };

        await propertyRepository.AddAsync(property, cancellationToken).ConfigureAwait(false);
        await propertyRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return property.ToDto();
    }
}

internal static class PropertyMappingExtensions
{
    public static PropertyDto ToDto(this Property property) =>
        new(
            property.Id,
            property.TenantId,
            property.ParentPropertyId,
            property.WardNo,
            property.PropertyNo,
            property.BuiltUpAreaSqFt,
            property.OpenPlotAreaSqFt,
            property.TotalAreaSqFt,
            property.BoundaryEast,
            property.BoundaryWest,
            property.BoundaryNorth,
            property.BoundarySouth,
            property.WaterConnectionCode,
            property.IsTaxExempt,
            property.IsActive);
}

