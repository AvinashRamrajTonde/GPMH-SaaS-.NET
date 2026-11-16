using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna9.Commands;

public sealed record ExecuteFyRolloverCommand(
    Guid SourceAssessmentYearId,
    string TargetYearLabel,
    bool CarryForwardCurrentTaxCalc = true) : IRequest<Namuna9RolloverResultDto>;

public sealed partial class ExecuteFyRolloverCommandValidator : AbstractValidator<ExecuteFyRolloverCommand>
{
    [GeneratedRegex(@"^\d{4}-\d{2}$")]
    private static partial Regex YearLabelRegex();

    public ExecuteFyRolloverCommandValidator()
    {
        RuleFor(x => x.SourceAssessmentYearId).NotEmpty();
        RuleFor(x => x.TargetYearLabel)
            .NotEmpty()
            .Must(lbl => !string.IsNullOrWhiteSpace(lbl) && YearLabelRegex().IsMatch(lbl.Trim()))
            .WithMessage("TargetYearLabel must match 'YYYY-YY' (e.g. '2027-28').");
    }
}

public sealed class ExecuteFyRolloverCommandHandler(
    INamuna9Repository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ExecuteFyRolloverCommand, Namuna9RolloverResultDto>
{
    public async Task<Namuna9RolloverResultDto> Handle(
        ExecuteFyRolloverCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can execute financial-year rollover.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        AssessmentYear sourceYear = await repository
            .GetYearAsync(request.SourceAssessmentYearId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Source assessment year '{request.SourceAssessmentYearId}' was not found.");

        if (sourceYear.IsLocked)
        {
            throw new DomainConflictException(
                "SOURCE_YEAR_ALREADY_LOCKED",
                $"Assessment year {sourceYear.YearLabel} is already locked from a previous rollover.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["assessmentYearId"] = sourceYear.Id,
                    ["yearLabel"] = sourceYear.YearLabel,
                });
        }

        string normalizedTargetLabel = request.TargetYearLabel.Trim();
        string[] parts = normalizedTargetLabel.Split('-');
        int targetStartYear = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int expectedEndSuffix = (targetStartYear + 1) % 100;
        int parsedEndSuffix = int.Parse(parts[1], CultureInfo.InvariantCulture);

        if (targetStartYear != sourceYear.StartYear + 1 || parsedEndSuffix != expectedEndSuffix)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["TargetYearLabel"] =
                [
                    $"Target financial year after '{sourceYear.YearLabel}' must be '{sourceYear.StartYear + 1}-{(sourceYear.StartYear + 2) % 100:D2}'.",
                ],
            });
        }

        AssessmentYear? existingTarget = await repository
            .FindYearByLabelAsync(normalizedTargetLabel, cancellationToken)
            .ConfigureAwait(false);

        if (existingTarget is not null && existingTarget.IsLocked)
        {
            throw new DomainConflictException(
                "TARGET_YEAR_LOCKED",
                $"Target assessment year '{normalizedTargetLabel}' is locked and cannot receive a rollover.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["assessmentYearId"] = existingTarget.Id,
                    ["yearLabel"] = existingTarget.YearLabel,
                });
        }

        Namuna9RolloverInputs inputs = await repository
            .LoadRolloverInputsAsync(sourceYear.Id, existingTarget?.Id, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateOnly targetCutoff = Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(targetStartYear);

        AssessmentYear targetYear = existingTarget ?? new AssessmentYear
        {
            TenantId = tenantId,
            YearLabel = normalizedTargetLabel,
            StartYear = targetStartYear,
            EndYear = targetStartYear + 1,
            IsActive = false,
            IsLocked = false,
        };

        int rolledOverCount = 0;
        decimal totalCarriedArrears = 0m;
        decimal totalNewCurrentDemand = 0m;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            // Deactivate & lock source first so unique active-year constraints stay valid.
            sourceYear.IsLocked = true;
            sourceYear.IsActive = false;
            await repository.SaveChangesAsync(ct).ConfigureAwait(false);

            targetYear.IsActive = true;
            targetYear.IsLocked = false;
            if (existingTarget is null)
            {
                await repository.AddYearAsync(targetYear, ct).ConfigureAwait(false);
            }

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);

            List<PropertyTaxCalc> newCalcs = [];
            List<PropertyDemand> newDemands = [];

            foreach (Property prop in inputs.Properties)
            {
                if (prop.IsDeleted || !prop.IsActive)
                {
                    continue;
                }

                DemandHeadBreakdown carriedHeads = DemandHeadBreakdown.Empty;
                decimal carriedArrears = 0m;

                if (inputs.SourceDemands.TryGetValue(prop.Id, out PropertyDemand? srcDemand))
                {
                    DemandHeadBreakdown srcArrears = DemandHeadBreakdown.FromJson(srcDemand.ArrearsByHeadJson);
                    DemandHeadBreakdown srcCurrent = Namuna9Support.ResolveCurrentHeads(
                        srcDemand,
                        inputs.SourceCalcs.GetValueOrDefault(prop.Id),
                        prop);
                    DemandCollectedBreakdown srcCollected = DemandCollectedBreakdown.FromJson(srcDemand.CollectedByHeadJson);

                    carriedHeads = DemandLedgerCalculator.ComputeRemaining(
                        srcArrears,
                        srcCurrent,
                        srcCollected,
                        srcDemand.NoticeFee,
                        srcDemand.WarrantFee,
                        srcDemand.PenaltyAmount,
                        srcDemand.RebateAmount,
                        srcDemand.PendingBalance);

                    carriedArrears = Math.Round(carriedHeads.Total, 2, MidpointRounding.AwayFromZero);
                }

                PropertyTaxCalc? targetCalc = inputs.TargetCalcs.GetValueOrDefault(prop.Id);
                if (targetCalc is null &&
                    request.CarryForwardCurrentTaxCalc &&
                    inputs.SourceCalcs.TryGetValue(prop.Id, out PropertyTaxCalc? srcCalc))
                {
                    targetCalc = new PropertyTaxCalc
                    {
                        TenantId = tenantId,
                        PropertyId = prop.Id,
                        AssessmentYearId = targetYear.Id,
                        BuildingTax = srcCalc.BuildingTax,
                        OpenLandTax = srcCalc.OpenLandTax,
                        LightingTax = srcCalc.LightingTax,
                        HealthTax = srcCalc.HealthTax,
                        SanitationTax = srcCalc.SanitationTax,
                        ToiletTax = srcCalc.ToiletTax,
                        WaterTax = srcCalc.WaterTax,
                        OtherTax = srcCalc.OtherTax,
                        TotalTax = srcCalc.TotalTax,
                        CalculationSnapshotJson = srcCalc.CalculationSnapshotJson,
                    };
                    newCalcs.Add(targetCalc);
                }

                decimal newCurrent = prop.IsTaxExempt || targetCalc is null
                    ? 0m
                    : targetCalc.TotalTax;

                DemandHeadBreakdown newCurrentHeads = prop.IsTaxExempt || targetCalc is null
                    ? DemandHeadBreakdown.Empty
                    : DemandHeadMapper.FromTaxHeads(
                        targetCalc.BuildingTax,
                        targetCalc.OpenLandTax,
                        targetCalc.LightingTax,
                        targetCalc.HealthTax,
                        targetCalc.SanitationTax,
                        targetCalc.ToiletTax,
                        targetCalc.WaterTax,
                        targetCalc.OtherTax,
                        prop.WaterConnectionCode);

                if (inputs.TargetDemands.TryGetValue(prop.Id, out PropertyDemand? targetDemand))
                {
                    targetDemand.ArrearsBalance = carriedArrears;
                    targetDemand.ArrearsByHeadJson = carriedHeads.ToJson();
                    targetDemand.CurrentDemand = newCurrent;
                    targetDemand.CurrentByHeadJson = newCurrentHeads.ToJson();
                    targetDemand.EarlyRebateEligible = newCurrent > 0m && !prop.IsTaxExempt;
                    targetDemand.RebateCutoffDate ??= targetCutoff;
                    targetDemand.PublishedAtUtc ??= now;
                    targetDemand.RecalculatePendingBalance();
                    targetDemand.Status = targetDemand.PendingBalance <= 0m
                        ? PropertyDemand.StatusSettled
                        : targetDemand.PaidAmount > 0m
                            ? PropertyDemand.StatusPartiallyPaid
                            : PropertyDemand.StatusPublished;
                }
                else
                {
                    PropertyDemand created = new()
                    {
                        TenantId = tenantId,
                        PropertyId = prop.Id,
                        AssessmentYearId = targetYear.Id,
                        ArrearsBalance = carriedArrears,
                        CurrentDemand = newCurrent,
                        RebateAmount = 0m,
                        PenaltyAmount = 0m,
                        NoticeFee = 0m,
                        WarrantFee = 0m,
                        PaidAmount = 0m,
                        PendingBalance = carriedArrears + newCurrent,
                        EarlyRebateEligible = newCurrent > 0m && !prop.IsTaxExempt,
                        EarlyRebateApplied = false,
                        RebateCutoffDate = targetCutoff,
                        PublishedAtUtc = now,
                        ArrearsByHeadJson = carriedHeads.ToJson(),
                        CurrentByHeadJson = newCurrentHeads.ToJson(),
                        CollectedByHeadJson = DemandCollectedBreakdown.Empty.ToJson(),
                        Status = carriedArrears + newCurrent <= 0m
                            ? PropertyDemand.StatusSettled
                            : PropertyDemand.StatusPublished,
                    };
                    newDemands.Add(created);
                }

                rolledOverCount++;
                totalCarriedArrears += carriedArrears;
                totalNewCurrentDemand += newCurrent;
            }

            if (newCalcs.Count > 0)
            {
                await repository.AddTaxCalcsAsync(newCalcs, ct).ConfigureAwait(false);
            }

            if (newDemands.Count > 0)
            {
                await repository.AddDemandsAsync(newDemands, ct).ConfigureAwait(false);
            }

            await repository.AddAuditLogsAsync(
                [
                    Namuna9Support.CreateAudit(
                        currentUserService,
                        dateTimeProvider,
                        tenantId,
                        nameof(AssessmentYear),
                        targetYear.Id.ToString(),
                        "FinancialYearRollover",
                        JsonSerializer.Serialize(new { sourceYear.Id, sourceYear.YearLabel, IsLocked = false }),
                        JsonSerializer.Serialize(new
                        {
                            SourceYearLabel = sourceYear.YearLabel,
                            TargetYearLabel = targetYear.YearLabel,
                            PropertiesRolledOver = rolledOverCount,
                            TotalCarriedArrears = totalCarriedArrears,
                            TotalCurrentDemand = totalNewCurrentDemand,
                        })),
                ],
                ct).ConfigureAwait(false);

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return new Namuna9RolloverResultDto(
            SourceYearId: sourceYear.Id,
            SourceYearLabel: sourceYear.YearLabel,
            TargetYearId: targetYear.Id,
            TargetYearLabel: targetYear.YearLabel,
            PropertiesRolledOver: rolledOverCount,
            TotalCarriedArrears: totalCarriedArrears,
            TotalCurrentDemand: totalNewCurrentDemand);
    }
}

