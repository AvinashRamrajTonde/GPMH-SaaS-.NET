using FluentValidation;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Queries;

/// <summary>Format values: A = general tax, B = water tax, C = combined (default).</summary>
public sealed record GetNamuna9RegisterQuery(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    string? Search = null,
    string? Format = "C",
    bool PendingOnly = false) : IRequest<Namuna9WorkspaceDto>;

public sealed class GetNamuna9RegisterQueryValidator : AbstractValidator<GetNamuna9RegisterQuery>
{
    public GetNamuna9RegisterQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(f => f is null or "A" or "B" or "C")
            .WithMessage("Format must be A (general tax), B (water tax) or C (combined).");
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.WardNo).MaximumLength(40);
    }
}

public sealed class GetNamuna9RegisterQueryHandler(INamuna9Repository repository)
    : IRequestHandler<GetNamuna9RegisterQuery, Namuna9WorkspaceDto>
{
    public async Task<Namuna9WorkspaceDto> Handle(GetNamuna9RegisterQuery request, CancellationToken cancellationToken)
    {
        AssessmentYear year = await Namuna9Support.ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AssessmentYear> years = await repository.ListYearsAsync(cancellationToken).ConfigureAwait(false);
        FormulaSetting? formula = await repository.GetActiveFormulaSettingAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Namuna9DemandContext> contexts = await repository
            .LoadContextsAsync(year.Id, request.WardNo, request.Search, null, cancellationToken)
            .ConfigureAwait(false);

        string format = string.IsNullOrWhiteSpace(request.Format) ? "C" : request.Format.Trim().ToUpperInvariant();

        List<Namuna9DemandRowDto> rows = [.. contexts.Select(Namuna9Support.ToRow)];

        if (request.PendingOnly)
        {
            rows = [.. rows.Where(r => r.PendingBalance > 0m)];
        }

        if (format == "B")
        {
            rows = [.. rows.Where(r =>
                r.GeneralWaterTax.Total + r.SpecialWaterTax.Total > 0m ||
                !string.Equals(r.WaterConnectionCode, "NONE", StringComparison.OrdinalIgnoreCase))];
        }

        decimal general = rows.Sum(r =>
            r.HouseTax.Total + r.LightingTax.Total + r.HealthTax.Total + r.SanitationTax.Total +
            r.ToiletTax.Total + r.OtherTax.Total);
        decimal water = rows.Sum(r => r.GeneralWaterTax.Total + r.SpecialWaterTax.Total);

        Namuna9KpisDto kpis = new(
            TotalDemand: rows.Sum(r => r.NetDemand),
            TotalCollected: rows.Sum(r => r.PaidAmount),
            TotalRebate: rows.Sum(r => r.RebateAmount),
            TotalBalanceDues: rows.Sum(r => r.PendingBalance),
            GeneralTaxDemand: general,
            WaterTaxDemand: water,
            PropertyCount: rows.Count);

        return new Namuna9WorkspaceDto(
            SelectedYear: Namuna9Support.ToDto(year),
            AssessmentYears: [.. years.Select(Namuna9Support.ToDto)],
            FormulaRates: Namuna9Support.ToRates(formula, year.StartYear),
            Format: format,
            Kpis: kpis,
            Items: rows);
    }
}

