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
    bool IsTaxExempt,
    string? StreetOrLaneName = null,
    string? CitySurveyOrGutNo = null,
    string? OccupierName = null,
    string? PropertyDescription = null,
    decimal OtherTax = 0m,
    string? OpenPlotConstructionCode = null,
    IReadOnlyList<PropertyOwnerRequestItem>? Owners = null,
    IReadOnlyList<PropertyFloorRequestItem>? Floors = null,
    bool CalculateTax = false) : IRequest<PropertyDto>;

public sealed class UpdatePropertyCommandValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.StreetOrLaneName).MaximumLength(160);
        RuleFor(x => x.CitySurveyOrGutNo).MaximumLength(60);
        RuleFor(x => x.OccupierName).MaximumLength(200);
        RuleFor(x => x.PropertyDescription).MaximumLength(300);
        RuleFor(x => x.BuiltUpAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OpenPlotAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OtherTax).GreaterThanOrEqualTo(0m);
        RuleFor(x => x)
            .Must(x => x.BuiltUpAreaSqFt + x.OpenPlotAreaSqFt > 0m || (x.Floors is { Count: > 0 }))
            .WithMessage("Total property area must be greater than zero.");
        RuleFor(x => x.WaterConnectionCode).NotEmpty().MaximumLength(24);
    }
}

public sealed class UpdatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    ISender sender)
    : IRequestHandler<UpdatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

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
        property.OtherTax = request.OtherTax;

        if (request.StreetOrLaneName is not null)
        {
            property.StreetOrLaneName = string.IsNullOrWhiteSpace(request.StreetOrLaneName)
                ? null
                : request.StreetOrLaneName.Trim();
        }
        if (request.CitySurveyOrGutNo is not null)
        {
            property.CitySurveyOrGutNo = string.IsNullOrWhiteSpace(request.CitySurveyOrGutNo)
                ? null
                : request.CitySurveyOrGutNo.Trim();
        }
        if (request.OccupierName is not null)
        {
            property.OccupierName = string.IsNullOrWhiteSpace(request.OccupierName)
                ? null
                : request.OccupierName.Trim();
        }
        if (request.PropertyDescription is not null)
        {
            property.PropertyDescription = string.IsNullOrWhiteSpace(request.PropertyDescription)
                ? null
                : request.PropertyDescription.Trim();
        }

        (
            IReadOnlyList<PropertyOwner>? owners,
            IReadOnlyList<PropertyConstruction>? constructions,
            PropertyTaxCalc? taxCalc,
            PropertyDemand? draftDemand,
            decimal computedBuiltUpSqFt,
            string? primaryOwnerNameMr,
            string? primaryOwnerNameEn) = await PropertyAssessmentHelper.PrepareAssessmentAsync(
                tenantId,
                property,
                request.Owners,
                request.Floors,
                request.CalculateTax || (request.Owners is { Count: > 0 }),
                request.OpenPlotConstructionCode,
                propertyRepository,
                citizenRepository,
                sender,
                cancellationToken).ConfigureAwait(false);

        if (request.Floors is { Count: > 0 })
        {
            property.BuiltUpAreaSqFt = computedBuiltUpSqFt;
            property.TotalAreaSqFt = computedBuiltUpSqFt + property.OpenPlotAreaSqFt;
        }

        await propertyRepository
            .PersistFullAssessmentAsync(
                property,
                owners,
                constructions,
                taxCalc,
                draftDemand,
                replaceOwnersAndConstructions: true,
                cancellationToken)
            .ConfigureAwait(false);

        return property.ToDto() with
        {
            PrimaryOwnerNameMr = primaryOwnerNameMr,
            PrimaryOwnerNameEn = primaryOwnerNameEn,
            TotalAnnualTax = taxCalc?.TotalTax,
            PendingBalance = draftDemand?.PendingBalance,
            FloorCount = constructions?.Count ?? 0,
        };
    }
}
