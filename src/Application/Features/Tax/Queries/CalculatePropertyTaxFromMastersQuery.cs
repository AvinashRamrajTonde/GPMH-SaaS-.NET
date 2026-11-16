using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Services;
using MediatR;

namespace GPMH.Application.Features.Tax.Queries;

public sealed record MasterFloorTaxRequest(
    string ConstructionTypeCode,
    string UsageCode,
    int ConstructionStartYear,
    decimal LengthFt = 0m,
    decimal WidthFt = 0m,
    decimal? AreaSqFt = null,
    decimal? AreaSqm = null);

public sealed record CalculatePropertyTaxFromMastersQuery(
    int AssessmentStartYear,
    IReadOnlyList<MasterFloorTaxRequest> Floors,
    decimal OpenAreaSqFt = 0m,
    decimal? OpenAreaSqm = null,
    string OpenPlotConstructionCode = "OPEN_PLOT",
    string WaterConnectionCode = "NONE",
    decimal OtherTax = 0m) : IRequest<CalculatedPropertyTaxDto>;

public sealed class CalculatePropertyTaxFromMastersQueryValidator
    : AbstractValidator<CalculatePropertyTaxFromMastersQuery>
{
    public CalculatePropertyTaxFromMastersQueryValidator()
    {
        RuleFor(x => x.AssessmentStartYear).InclusiveBetween(1950, 2100);
        RuleFor(x => x.OpenAreaSqFt).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OpenAreaSqm).GreaterThanOrEqualTo(0m).When(x => x.OpenAreaSqm.HasValue);
        RuleFor(x => x.OtherTax).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.WaterConnectionCode).NotEmpty().MaximumLength(32);

        RuleFor(x => x)
            .Must(x => x.Floors.Count > 0 || x.OpenAreaSqFt > 0m || (x.OpenAreaSqm ?? 0m) > 0m)
            .WithMessage("Supply at least one built floor or positive open plot area.");

        RuleForEach(x => x.Floors).ChildRules(floor =>
        {
            floor.RuleFor(f => f.ConstructionTypeCode).NotEmpty().MaximumLength(40);
            floor.RuleFor(f => f.UsageCode).NotEmpty().MaximumLength(32);
            floor.RuleFor(f => f.ConstructionStartYear).InclusiveBetween(1850, 2100);
            floor.RuleFor(f => f.LengthFt).GreaterThanOrEqualTo(0m);
            floor.RuleFor(f => f.WidthFt).GreaterThanOrEqualTo(0m);
            floor.RuleFor(f => f.AreaSqFt).GreaterThanOrEqualTo(0m).When(f => f.AreaSqFt.HasValue);
            floor.RuleFor(f => f.AreaSqm).GreaterThanOrEqualTo(0m).When(f => f.AreaSqm.HasValue);
            floor.RuleFor(f => f)
                .Must(f => (f.LengthFt > 0m && f.WidthFt > 0m) || (f.AreaSqFt ?? 0m) > 0m || (f.AreaSqm ?? 0m) > 0m)
                .WithMessage("Each floor requires positive Length x Width or positive explicit area.");
        });
    }
}

public sealed class CalculatePropertyTaxFromMastersQueryHandler(ISender sender)
    : IRequestHandler<CalculatePropertyTaxFromMastersQuery, CalculatedPropertyTaxDto>
{
    public async Task<CalculatedPropertyTaxDto> Handle(
        CalculatePropertyTaxFromMastersQuery request,
        CancellationToken cancellationToken)
    {
        FormulaWorkspaceDto workspace = await sender
            .Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken)
            .ConfigureAwait(false);

        FormulaSettingDto activeSetting = workspace.ActiveSetting
            ?? throw new NotFoundException("Active statutory FormulaSetting was not found for this tenant.");

        IReadOnlyList<DepreciationSlabDto> slabs = workspace.DepreciationSlabs ?? [];
        if (slabs.Count == 0)
        {
            throw new NotFoundException("Depreciation slabs are not configured for this tenant.");
        }

        IReadOnlyList<DepreciationSlabInput> hutMudSlabs = [.. slabs
            .OrderBy(s => s.AgeFrom)
            .ThenBy(s => s.AgeTo)
            .Select(s => new DepreciationSlabInput(s.AgeFrom, s.AgeTo, s.HutMudPercent))];

        IReadOnlyList<DepreciationSlabInput> stoneBrickSlabs = [.. slabs
            .OrderBy(s => s.AgeFrom)
            .ThenBy(s => s.AgeTo)
            .Select(s => new DepreciationSlabInput(s.AgeFrom, s.AgeTo, s.StoneBrickRccPercent))];

        Dictionary<string, ConstructionTypeMasterDto> constructionByCode = workspace.ConstructionTypes
            .Where(c => c.IsActive)
            .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, UsageWeightageDto> usageByCode = (workspace.UsageWeightages ?? [])
            .Where(u => u.IsActive)
            .ToDictionary(u => u.Code, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, WaterTaxMasterDto> waterByCode = (workspace.WaterTaxes ?? [])
            .Where(w => w.IsActive)
            .ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        List<FloorTaxInput> domainFloors = new(request.Floors.Count);
        decimal totalBuiltUpSqFt = 0m;

        for (int i = 0; i < request.Floors.Count; i++)
        {
            MasterFloorTaxRequest reqFloor = request.Floors[i];
            if (!constructionByCode.TryGetValue(reqFloor.ConstructionTypeCode.Trim(), out ConstructionTypeMasterDto? ct))
            {
                errors[$"Floors[{i}].ConstructionTypeCode"] =
                    [$"Active construction type '{reqFloor.ConstructionTypeCode}' not found."];
                continue;
            }

            if (!usageByCode.TryGetValue(reqFloor.UsageCode.Trim(), out UsageWeightageDto? uw))
            {
                errors[$"Floors[{i}].UsageCode"] =
                    [$"Active usage weightage '{reqFloor.UsageCode}' not found."];
                continue;
            }

            if (reqFloor.ConstructionStartYear > request.AssessmentStartYear)
            {
                errors[$"Floors[{i}].ConstructionStartYear"] =
                    [$"Construction year ({reqFloor.ConstructionStartYear}) cannot exceed assessment year ({request.AssessmentStartYear})."];
                continue;
            }

            decimal floorSqFt;
            decimal lengthFt;
            decimal widthFt;
            decimal? explicitSqm = reqFloor.AreaSqm;

            if (reqFloor.LengthFt > 0m && reqFloor.WidthFt > 0m)
            {
                lengthFt = reqFloor.LengthFt;
                widthFt = reqFloor.WidthFt;
                floorSqFt = lengthFt * widthFt;
            }
            else if (reqFloor.AreaSqFt is > 0m)
            {
                floorSqFt = reqFloor.AreaSqFt.Value;
                lengthFt = floorSqFt;
                widthFt = 1m;
            }
            else
            {
                decimal sqm = reqFloor.AreaSqm ?? 0m;
                floorSqFt = sqm / CapitalValueTaxCalculator.SqFtToSqM;
                lengthFt = 0m;
                widthFt = 0m;
            }

            totalBuiltUpSqFt += floorSqFt;

            IReadOnlyList<DepreciationSlabInput> floorCurve = string.Equals(ct.Category, "HUT_MUD", StringComparison.OrdinalIgnoreCase)
                ? hutMudSlabs
                : stoneBrickSlabs;

            domainFloors.Add(new FloorTaxInput(
                LengthFt: lengthFt,
                WidthFt: widthFt,
                AreaSqm: explicitSqm,
                LandAnnualValueRate: ct.LandAnnualValueRate,
                ConstructionReadyReckonerRate: ct.ConstructionReadyReckonerRate,
                UsageWeightage: uw.Weightage,
                TaxRate: ct.TaxRate,
                ConstructionStartYear: reqFloor.ConstructionStartYear,
                DepreciationSlabsOverride: floorCurve));
        }

        string normalizedWaterCode = request.WaterConnectionCode.Trim().ToUpperInvariant();
        if (!waterByCode.TryGetValue(normalizedWaterCode, out WaterTaxMasterDto? waterMaster))
        {
            errors[nameof(request.WaterConnectionCode)] =
                [$"Active water connection type '{request.WaterConnectionCode}' not found."];
        }

        decimal openSqm = request.OpenAreaSqm ?? (request.OpenAreaSqFt * CapitalValueTaxCalculator.SqFtToSqM);
        if (!request.OpenAreaSqm.HasValue && activeSetting.RoundArea && openSqm > 0m)
        {
            openSqm = Math.Round(openSqm, 2, MidpointRounding.AwayFromZero);
        }

        decimal openSqFt = request.OpenAreaSqFt > 0m
            ? request.OpenAreaSqFt
            : (openSqm > 0m ? openSqm / CapitalValueTaxCalculator.SqFtToSqM : 0m);

        decimal openLandRate = 0m;
        decimal openTaxRate = 0m;
        if (openSqm > 0m)
        {
            string openCode = string.IsNullOrWhiteSpace(request.OpenPlotConstructionCode)
                ? "OPEN_PLOT"
                : request.OpenPlotConstructionCode.Trim();

            if (!constructionByCode.TryGetValue(openCode, out ConstructionTypeMasterDto? openMaster))
            {
                errors[nameof(request.OpenPlotConstructionCode)] =
                    [$"Active open-plot rate code '{openCode}' not found."];
            }
            else
            {
                openLandRate = openMaster.LandAnnualValueRate;
                openTaxRate = openMaster.TaxRate;
            }
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        (AreaSlabTaxInput areaInput, string slabRangeLabel) = ResolveAreaSlabTaxes(
            totalBuiltUpSqFt,
            openSqFt,
            activeSetting.ApplyLightingHealthOnOpenPlot,
            workspace.AreaSlabTaxes ?? []);

        PropertyTaxCalculationInput calcInput = new(
            AssessmentStartYear: request.AssessmentStartYear,
            Floors: domainFloors,
            DepreciationSlabs: stoneBrickSlabs,
            AreaSlabTaxes: areaInput,
            WaterTax: waterMaster!.Amount,
            OtherTax: request.OtherTax,
            OpenAreaSqm: openSqm,
            OpenPlotLandRate: openLandRate,
            OpenPlotTaxRate: openTaxRate,
            TaxDivisor: activeSetting.TaxDivisor,
            RoundAreaToTwoDecimals: activeSetting.RoundArea);

        PropertyTaxCalculationResult result = CapitalValueTaxCalculator.Calculate(calcInput);
        decimal totalBuiltUpSqm = result.FloorBreakdowns.Sum(b => b.AreaSqm);

        return new CalculatedPropertyTaxDto(
            AssessmentStartYear: request.AssessmentStartYear,
            RatePeriodName: activeSetting.RatePeriodName,
            TaxDivisor: activeSetting.TaxDivisor,
            TotalBuiltUpSqFt: Math.Round(totalBuiltUpSqFt, 2, MidpointRounding.AwayFromZero),
            TotalBuiltUpSqM: totalBuiltUpSqm,
            OpenAreaSqFt: Math.Round(openSqFt, 2, MidpointRounding.AwayFromZero),
            OpenAreaSqM: openSqm,
            WaterConnectionCode: waterMaster.Code,
            AppliedAreaSlabRange: slabRangeLabel,
            Calculation: result);
    }

    private static (AreaSlabTaxInput Taxes, string RangeLabel) ResolveAreaSlabTaxes(
        decimal builtUpSqFt,
        decimal openSqFt,
        bool applyLightingHealthOnOpenPlot,
        IReadOnlyList<AreaSlabTaxDto> areaSlabs)
    {
        if (areaSlabs.Count == 0)
        {
            return (new AreaSlabTaxInput(0m, 0m, 0m, 0m), "none");
        }

        IReadOnlyList<AreaSlabTaxDto> ordered = [.. areaSlabs.OrderBy(s => s.AreaFrom).ThenBy(s => s.AreaTo)];

        if (builtUpSqFt > 0m)
        {
            int lookupSqFt = Math.Max(1, (int)Math.Round(builtUpSqFt, 0, MidpointRounding.AwayFromZero));
            AreaSlabTaxDto matched = ordered.FirstOrDefault(s => lookupSqFt >= s.AreaFrom && lookupSqFt <= s.AreaTo)
                ?? ordered[^1];
            return (
                new AreaSlabTaxInput(matched.LightingTax, matched.HealthTax, matched.SanitationTax, matched.ToiletTax),
                $"{matched.AreaFrom}-{matched.AreaTo}");
        }

        if (openSqFt > 0m && applyLightingHealthOnOpenPlot)
        {
            int lookupSqFt = Math.Max(1, (int)Math.Round(openSqFt, 0, MidpointRounding.AwayFromZero));
            AreaSlabTaxDto matched = ordered.FirstOrDefault(s => lookupSqFt >= s.AreaFrom && lookupSqFt <= s.AreaTo)
                ?? ordered[^1];
            return (
                new AreaSlabTaxInput(matched.LightingTax, matched.HealthTax, 0m, 0m),
                $"{matched.AreaFrom}-{matched.AreaTo} (open-plot)");
        }

        return (new AreaSlabTaxInput(0m, 0m, 0m, 0m), "exempt/none");
    }
}

