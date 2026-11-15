using FluentValidation;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Tax.Queries;

public sealed record PreviewCapitalTaxQuery(PropertyTaxCalculationInput Input)
    : IRequest<PropertyTaxCalculationResult>;

public sealed class PreviewCapitalTaxQueryValidator : AbstractValidator<PreviewCapitalTaxQuery>
{
    public PreviewCapitalTaxQueryValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.AssessmentStartYear).InclusiveBetween(1950, 2100);
        RuleFor(x => x.Input.TaxDivisor).GreaterThan(0m);
        RuleFor(x => x.Input.DepreciationSlabs).NotEmpty();
        RuleForEach(x => x.Input.Floors).ChildRules(floor =>
        {
            floor.RuleFor(f => f.LandAnnualValueRate).GreaterThanOrEqualTo(0m);
            floor.RuleFor(f => f.ConstructionReadyReckonerRate).GreaterThanOrEqualTo(0m);
            floor.RuleFor(f => f.UsageWeightage).GreaterThan(0m);
            floor.RuleFor(f => f.TaxRate).GreaterThanOrEqualTo(0m);
        });
    }
}

public sealed class PreviewCapitalTaxQueryHandler
    : IRequestHandler<PreviewCapitalTaxQuery, PropertyTaxCalculationResult>
{
    public Task<PropertyTaxCalculationResult> Handle(
        PreviewCapitalTaxQuery request,
        CancellationToken cancellationToken)
    {
        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(request.Input);
        return Task.FromResult(result);
    }
}

