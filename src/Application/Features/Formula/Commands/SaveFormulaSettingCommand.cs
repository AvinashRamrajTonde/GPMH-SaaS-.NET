using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record SaveFormulaSettingCommand(
    Guid? Id,
    string RatePeriodName,
    int RateStartYear,
    int RateEndYear,
    decimal TaxDivisor,
    int NoticeGraceDays,
    decimal EarlyRebatePct,
    decimal LatePenaltyPct,
    bool ApplyLightingHealthOnOpenPlot,
    bool RoundArea,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ResolutionReason,
    bool CreateNewPeriod = false) : IRequest<FormulaSettingDto>;

public sealed class SaveFormulaSettingCommandValidator : AbstractValidator<SaveFormulaSettingCommand>
{
    public SaveFormulaSettingCommandValidator()
    {
        RuleFor(x => x.RatePeriodName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.RateStartYear).InclusiveBetween(1950, 2100);
        RuleFor(x => x.RateEndYear)
            .GreaterThanOrEqualTo(x => x.RateStartYear)
            .WithMessage("Rate end year must be greater than or equal to start year.")
            .LessThanOrEqualTo(2105);
        RuleFor(x => x.TaxDivisor).GreaterThan(0m).LessThanOrEqualTo(1_000_000m);
        RuleFor(x => x.NoticeGraceDays).InclusiveBetween(1, 365);
        RuleFor(x => x.EarlyRebatePct).InclusiveBetween(0m, 100m);
        RuleFor(x => x.LatePenaltyPct).InclusiveBetween(0m, 100m);
        RuleFor(x => x.ResolutionNo)
            .NotEmpty()
            .WithMessage("GP Resolution No is required for statutory formula changes.")
            .MaximumLength(80);
        RuleFor(x => x.ResolutionReason)
            .NotEmpty()
            .WithMessage("Resolution reason / remarks are required for audit traceability.")
            .MaximumLength(400);
    }
}

public sealed class SaveFormulaSettingCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveFormulaSettingCommand, FormulaSettingDto>
{
    public async Task<FormulaSettingDto> Handle(
        SaveFormulaSettingCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string normalizedPeriod = request.RatePeriodName.Trim();
        string resolutionNo = request.ResolutionNo.Trim();
        string reason = request.ResolutionReason.Trim();

        FormulaSetting? target;
        bool isNew = false;

        if (request.Id.HasValue)
        {
            target = await formulaRepository
                .GetFormulaSettingByIdAsync(request.Id.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Formula setting '{request.Id.Value}' was not found.");

            FormulaSetting? conflict = await formulaRepository
                .GetFormulaSettingByPeriodNameAsync(normalizedPeriod, cancellationToken)
                .ConfigureAwait(false);
            if (conflict is not null && conflict.Id != target.Id)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.RatePeriodName)] = [$"Rate period '{normalizedPeriod}' already exists for this Gram Panchayat."],
                });
            }
        }
        else if (request.CreateNewPeriod)
        {
            FormulaSetting? existingByPeriod = await formulaRepository
                .GetFormulaSettingByPeriodNameAsync(normalizedPeriod, cancellationToken)
                .ConfigureAwait(false);
            if (existingByPeriod is not null)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.RatePeriodName)] = [$"Rate period '{normalizedPeriod}' already exists for this Gram Panchayat."],
                });
            }

            target = new FormulaSetting
            {
                TenantId = tenantId,
                RatePeriodName = normalizedPeriod,
            };
            isNew = true;
        }
        else
        {
            target = await formulaRepository
                .GetFormulaSettingByPeriodNameAsync(normalizedPeriod, cancellationToken)
                .ConfigureAwait(false)
                ?? await formulaRepository
                    .GetActiveFormulaSettingAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (target is null)
            {
                target = new FormulaSetting
                {
                    TenantId = tenantId,
                    RatePeriodName = normalizedPeriod,
                };
                isNew = true;
            }
        }

        target.RatePeriodName = normalizedPeriod;
        target.RateStartYear = request.RateStartYear;
        target.RateEndYear = request.RateEndYear;
        target.TaxDivisor = request.TaxDivisor;
        target.NoticeGraceDays = request.NoticeGraceDays;
        target.EarlyRebatePct = request.EarlyRebatePct;
        target.LatePenaltyPct = request.LatePenaltyPct;
        target.ApplyLightingHealthOnOpenPlot = request.ApplyLightingHealthOnOpenPlot;
        target.RoundArea = request.RoundArea;
        target.IsActive = request.IsActive;
        target.LastResolutionNo = resolutionNo;
        target.LastResolutionDate = request.ResolutionDate;
        target.LastChangeReason = reason;

        if (isNew)
        {
            await formulaRepository.AddFormulaSettingAsync(target, cancellationToken).ConfigureAwait(false);
        }

        // Keep at most one active rate period per Gram Panchayat.
        if (target.IsActive)
        {
            IReadOnlyList<FormulaSetting> allSettings = await formulaRepository
                .ListFormulaSettingsAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (FormulaSetting other in allSettings)
            {
                if (other.Id != target.Id && other.IsActive)
                {
                    other.IsActive = false;
                }
            }
        }

        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        return target.ToDto();
    }
}

