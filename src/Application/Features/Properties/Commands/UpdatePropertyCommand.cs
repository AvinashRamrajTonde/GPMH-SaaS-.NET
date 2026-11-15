using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Properties.Commands;

public sealed record UpdatePropertyCommand(
    Guid Id,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    string? BoundaryEast,
    string? BoundaryWest,
    string? BoundaryNorth,
    string? BoundarySouth,
    string WaterConnectionCode,
    bool IsTaxExempt) : IRequest<PropertyDto>;

public sealed class UpdatePropertyCommandValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.BuiltUpAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OpenPlotAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.BuiltUpAreaSqFt + x.OpenPlotAreaSqFt)
            .GreaterThan(0m)
            .WithMessage("Total property area must be greater than zero.");
        RuleFor(x => x.WaterConnectionCode).NotEmpty().MaximumLength(24);
    }
}

public sealed class UpdatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    ITenantContext tenantContext)
    : IRequestHandler<UpdatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        Property property = await propertyRepository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Property), request.Id);

        property.BuiltUpAreaSqFt = request.BuiltUpAreaSqFt;
        property.OpenPlotAreaSqFt = request.OpenPlotAreaSqFt;
        property.TotalAreaSqFt = request.BuiltUpAreaSqFt + request.OpenPlotAreaSqFt;
        property.BoundaryEast = request.BoundaryEast?.Trim();
        property.BoundaryWest = request.BoundaryWest?.Trim();
        property.BoundaryNorth = request.BoundaryNorth?.Trim();
        property.BoundarySouth = request.BoundarySouth?.Trim();
        property.WaterConnectionCode = request.WaterConnectionCode.Trim().ToUpperInvariant();
        property.IsTaxExempt = request.IsTaxExempt;

        await propertyRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return property.ToDto();
    }
}

