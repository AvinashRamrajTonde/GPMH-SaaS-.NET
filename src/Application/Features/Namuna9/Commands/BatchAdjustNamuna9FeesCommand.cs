using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Commands;

public sealed record BatchAdjustNamuna9FeesCommand(
    Guid AssessmentYearId,
    IReadOnlyList<Namuna9FeeAdjustmentItem> Adjustments,
    string? Remarks = null) : IRequest<Namuna9AdjustResultDto>;

public sealed class Namuna9FeeAdjustmentItemValidator : AbstractValidator<Namuna9FeeAdjustmentItem>
{
    public Namuna9FeeAdjustmentItemValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.NoticeFee).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.WarrantFee).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.PenaltyAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.RebateAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Remarks).MaximumLength(600);
    }
}

public sealed class BatchAdjustNamuna9FeesCommandValidator : AbstractValidator<BatchAdjustNamuna9FeesCommand>
{
    public BatchAdjustNamuna9FeesCommandValidator()
    {
        RuleFor(x => x.AssessmentYearId).NotEmpty();
        RuleFor(x => x.Remarks).MaximumLength(600);
        RuleFor(x => x.Adjustments)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one fee/rebate adjustment row must be submitted.");
        RuleForEach(x => x.Adjustments).SetValidator(new Namuna9FeeAdjustmentItemValidator());
    }
}

public sealed class BatchAdjustNamuna9FeesCommandHandler(
    INamuna9Repository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BatchAdjustNamuna9FeesCommand, Namuna9AdjustResultDto>
{
    public async Task<Namuna9AdjustResultDto> Handle(
        BatchAdjustNamuna9FeesCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can modify Namuna 9 statutory fees, penalties, or rebates.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        AssessmentYear year = await Namuna9Support
            .ResolveYearAsync(repository, request.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);
        Namuna9Support.EnsureNotLocked(year);

        HashSet<Guid> targetIds = [.. request.Adjustments.Select(a => a.PropertyId)];
        IReadOnlyList<Namuna9DemandContext> contexts = await repository
            .LoadContextsAsync(year.Id, null, null, targetIds, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Namuna9DemandContext> byProperty = contexts.ToDictionary(c => c.Property.Id);

        List<Namuna9AdjustedDemandDto> results = [];
        List<AuditLog> audits = [];

        foreach (Namuna9FeeAdjustmentItem item in request.Adjustments)
        {
            if (!byProperty.TryGetValue(item.PropertyId, out Namuna9DemandContext? ctx))
            {
                throw new NotFoundException(
                    $"Namuna 9 demand row for property '{item.PropertyId}' in year '{year.YearLabel}' was not found.");
            }

            PropertyDemand d = ctx.Demand;

            if (item.RebateAmount > d.CurrentDemand)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["RebateAmount"] =
                    [
                        $"Rebate amount ({item.RebateAmount:F2}) for {ctx.Property.WardNo}/{ctx.Property.PropertyNo} cannot exceed current demand ({d.CurrentDemand:F2}).",
                    ],
                });
            }

            string oldJson = JsonSerializer.Serialize(new
            {
                d.NoticeFee,
                d.WarrantFee,
                d.PenaltyAmount,
                d.RebateAmount,
                d.PendingBalance,
                d.Status,
                d.DemandRemarks,
            });

            d.NoticeFee = Math.Round(item.NoticeFee, 2, MidpointRounding.AwayFromZero);
            d.WarrantFee = Math.Round(item.WarrantFee, 2, MidpointRounding.AwayFromZero);
            d.PenaltyAmount = Math.Round(item.PenaltyAmount, 2, MidpointRounding.AwayFromZero);
            d.RebateAmount = Math.Round(item.RebateAmount, 2, MidpointRounding.AwayFromZero);
            d.EarlyRebateApplied = d.RebateAmount > 0m;

            if (!string.IsNullOrWhiteSpace(item.Remarks))
            {
                d.DemandRemarks = item.Remarks.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(request.Remarks))
            {
                d.DemandRemarks = request.Remarks.Trim();
            }

            DemandCollectedBreakdown collected = DemandCollectedBreakdown.FromJson(d.CollectedByHeadJson) with
            {
                RebateGranted = d.RebateAmount,
            };
            d.CollectedByHeadJson = collected.ToJson();

            d.RecalculatePendingBalance();
            d.PublishedAtUtc ??= dateTimeProvider.UtcNow;
            d.Status = d.PendingBalance <= 0m
                ? PropertyDemand.StatusSettled
                : d.PaidAmount > 0m
                    ? PropertyDemand.StatusPartiallyPaid
                    : PropertyDemand.StatusPublished;

            string newJson = JsonSerializer.Serialize(new
            {
                d.NoticeFee,
                d.WarrantFee,
                d.PenaltyAmount,
                d.RebateAmount,
                d.PendingBalance,
                d.Status,
                d.DemandRemarks,
            });

            audits.Add(Namuna9Support.CreateAudit(
                currentUserService,
                dateTimeProvider,
                tenantId,
                nameof(PropertyDemand),
                d.Id.ToString(),
                "Namuna9FeesAdjusted",
                oldJson,
                newJson));

            results.Add(new Namuna9AdjustedDemandDto(
                d.Id,
                d.PropertyId,
                d.TotalNetDemand,
                d.PendingBalance,
                d.Status));
        }

        await repository.AddAuditLogsAsync(audits, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new Namuna9AdjustResultDto(year.YearLabel, results.Count, results);
    }
}

