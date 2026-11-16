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
    Guid? ParentPropertyId = null,
    string? StreetOrLaneName = null,
    string? CitySurveyOrGutNo = null,
    string? OccupierName = null,
    string? PropertyDescription = null,
    decimal OtherTax = 0m,
    string? OpenPlotConstructionCode = null,
    IReadOnlyList<PropertyOwnerRequestItem>? Owners = null,
    IReadOnlyList<PropertyFloorRequestItem>? Floors = null,
    bool CalculateTax = false) : IRequest<PropertyDto>;

public sealed class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyCommandValidator()
    {
        RuleFor(x => x.WardNo).NotEmpty().MaximumLength(40);
        RuleFor(x => x.PropertyNo).NotEmpty().MaximumLength(40);
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

public sealed class CreatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    ISender sender)
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
            StreetOrLaneName = string.IsNullOrWhiteSpace(request.StreetOrLaneName) ? null : request.StreetOrLaneName.Trim(),
            CitySurveyOrGutNo = string.IsNullOrWhiteSpace(request.CitySurveyOrGutNo) ? null : request.CitySurveyOrGutNo.Trim(),
            OccupierName = string.IsNullOrWhiteSpace(request.OccupierName) ? null : request.OccupierName.Trim(),
            PropertyDescription = string.IsNullOrWhiteSpace(request.PropertyDescription) ? null : request.PropertyDescription.Trim(),
            BuiltUpAreaSqFt = request.BuiltUpAreaSqFt,
            OpenPlotAreaSqFt = request.OpenPlotAreaSqFt,
            TotalAreaSqFt = request.BuiltUpAreaSqFt + request.OpenPlotAreaSqFt,
            BoundaryEast = request.BoundaryEast?.Trim(),
            BoundaryWest = request.BoundaryWest?.Trim(),
            BoundaryNorth = request.BoundaryNorth?.Trim(),
            BoundarySouth = request.BoundarySouth?.Trim(),
            WaterConnectionCode = request.WaterConnectionCode.Trim().ToUpperInvariant(),
            OtherTax = request.OtherTax,
            IsTaxExempt = request.IsTaxExempt,
            Status = "Active",
        };

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

        await propertyRepository.AddAsync(property, cancellationToken).ConfigureAwait(false);
        await propertyRepository
            .PersistFullAssessmentAsync(
                property,
                owners,
                constructions,
                taxCalc,
                draftDemand,
                replaceOwnersAndConstructions: false,
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
