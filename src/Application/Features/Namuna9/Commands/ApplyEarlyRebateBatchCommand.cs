using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Commands;

public sealed record ApplyEarlyRebateBatchCommand(
    Guid AssessmentYearId,
    DateOnly ApplicationDate,
    DateOnly? CutoffDate = null,
    IReadOnlyList<Guid>? PropertyIds = null) : IRequest<Namuna9RebateResultDto>;

public sealed class ApplyEarlyRebateBatchCommandValidator : AbstractValidator<ApplyEarlyRebateBatchCommand>
{
    public ApplyEarlyRebateBatchCommandValidator()
    {
        RuleFor(x => x.AssessmentYearId).NotEmpty();
    }
}

public sealed class ApplyEarlyRebateBatchCommandHandler(
    INamuna9Repository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ApplyEarlyRebateBatchCommand, Namuna9RebateResultDto>
{
    public async Task<Namuna9RebateResultDto> Handle(
        ApplyEarlyRebateBatchCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can sanction early payment rebates.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        AssessmentYear year = await Namuna9Support
            .ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);
        Namuna9Support.EnsureNotLocked(year);

        FormulaSetting? formula = await repository.GetActiveFormulaSettingAsync(cancellationToken).ConfigureAwait(false);
        decimal rebatePct = formula?.EarlyRebatePct ?? Namuna9RebateAndPenaltyCalculator.DefaultEarlyRebatePct;
        DateOnly cutoff = request.CutoffDate ?? Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(year.StartYear);

        IReadOnlyList<Namuna9DemandContext> contexts = await repository
            .LoadContextsAsync(year.Id, null, null, request.PropertyIds, cancellationToken)
            .ConfigureAwait(false);

        int applied = 0;
        int skipped = 0;
        decimal totalRebate = 0m;
        List<AuditLog> audits = [];

        foreach (Namuna9DemandContext ctx in contexts)
        {
            PropertyDemand d = ctx.Demand;
            if (ctx.Property.IsTaxExempt ||
                d.CurrentDemand <= 0m ||
                string.Equals(d.Status, PropertyDemand.StatusSettled, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            DemandHeadBreakdown currentHeads = Namuna9Support.ResolveCurrentHeads(d, ctx.TaxCalc, ctx.Property);
            decimal eligibleGeneralTax = currentHeads.GeneralTaxTotal > 0m
                ? Math.Min(currentHeads.GeneralTaxTotal, d.CurrentDemand)
                : d.CurrentDemand;

            decimal rebate = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
                eligibleGeneralTax,
                request.ApplicationDate,
                year.StartYear,
                rebatePct,
                cutoff);

            rebate = Math.Min(rebate, d.CurrentDemand);
            if (rebate <= 0m)
            {
                skipped++;
                continue;
            }

            string oldJson = JsonSerializer.Serialize(new { d.RebateAmount, d.PendingBalance, d.EarlyRebateApplied });

            d.RebateAmount = rebate;
            d.EarlyRebateEligible = true;
            d.EarlyRebateApplied = true;
            d.RebateCutoffDate = cutoff;
            d.PublishedAtUtc ??= dateTimeProvider.UtcNow;

            DemandCollectedBreakdown collected = DemandCollectedBreakdown.FromJson(d.CollectedByHeadJson) with
            {
                RebateGranted = rebate,
            };
            d.CollectedByHeadJson = collected.ToJson();

            d.RecalculatePendingBalance();
            d.Status = d.PendingBalance <= 0m
                ? PropertyDemand.StatusSettled
                : d.PaidAmount > 0m
                    ? PropertyDemand.StatusPartiallyPaid
                    : PropertyDemand.StatusPublished;

            audits.Add(Namuna9Support.CreateAudit(
                currentUserService,
                dateTimeProvider,
                tenantId,
                nameof(PropertyDemand),
                d.Id.ToString(),
                "Namuna9EarlyRebateApplied",
                oldJson,
                JsonSerializer.Serialize(new
                {
                    d.RebateAmount,
                    d.PendingBalance,
                    ApplicationDate = request.ApplicationDate,
                    CutoffDate = cutoff,
                    RebatePct = rebatePct,
                })));

            applied++;
            totalRebate += rebate;
        }

        if (applied > 0)
        {
            await repository.AddAuditLogsAsync(audits, cancellationToken).ConfigureAwait(false);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new Namuna9RebateResultDto(
            year.YearLabel,
            request.ApplicationDate,
            cutoff,
            rebatePct,
            applied,
            skipped,
            totalRebate);
    }
}

