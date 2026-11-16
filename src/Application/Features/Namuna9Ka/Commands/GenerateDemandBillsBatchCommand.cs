using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna9Ka;
using GPMH.Application.Features.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna9Ka.Commands;

public sealed record GenerateDemandBillsBatchCommand(
    Guid? AssessmentYearId = null,
    string? WardNo = null,
    IReadOnlyList<Guid>? PropertyIds = null,
    DateOnly? IssueDate = null,
    bool RegenerateIfChanged = false) : IRequest<Namuna9KaGenerateResultDto>;

public sealed class GenerateDemandBillsBatchCommandValidator : AbstractValidator<GenerateDemandBillsBatchCommand>
{
    public GenerateDemandBillsBatchCommandValidator()
    {
        RuleFor(x => x.WardNo).MaximumLength(40);
        RuleFor(x => x.PropertyIds)
            .Must(ids => ids is null || ids.Count <= 5000)
            .WithMessage("At most 5,000 properties can be generated per batch.");
    }
}

public sealed class GenerateDemandBillsBatchCommandHandler(
    INamuna9KaRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GenerateDemandBillsBatchCommand, Namuna9KaGenerateResultDto>
{
    public async Task<Namuna9KaGenerateResultDto> Handle(
        GenerateDemandBillsBatchCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak or ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, or Super Admin can generate statutory Namuna 9-Ka demand bills.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");
        Guid userId = currentUserService.UserId ?? Guid.Empty;

        Namuna9KaGenerateResultDto? output = null;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            Namuna9KaBillGenContext ctx = await repository
                .LoadGenerationContextAsync(
                    tenantId,
                    request.AssessmentYearId,
                    request.WardNo,
                    request.PropertyIds,
                    ct)
                .ConfigureAwait(false);

            Namuna9Support.EnsureNotLocked(ctx.Year);

            DateTimeOffset nowUtc = dateTimeProvider.UtcNow;
            DateOnly issueDate = request.IssueDate ?? DateOnly.FromDateTime(nowUtc.UtcDateTime);
            int graceDays = ctx.Formula?.NoticeGraceDays > 0
                ? ctx.Formula.NoticeGraceDays
                : Namuna9RebateAndPenaltyCalculator.DefaultNoticeGraceDays;
            DateOnly dueDate = issueDate.AddDays(graceDays);

            List<BillGenerationCandidate> toCreate = [];
            List<Namuna9KaGeneratedBillRefDto> reusedRefs = [];
            int supersededCount = 0;

            foreach (Property prop in ctx.Properties)
            {
                if (!ctx.DemandsByProperty.TryGetValue(prop.Id, out PropertyDemand? demand))
                {
                    continue;
                }

                DemandHeadBreakdown arrears = DemandHeadBreakdown.FromJson(demand.ArrearsByHeadJson);
                ctx.TaxCalcsByProperty.TryGetValue(prop.Id, out PropertyTaxCalc? calc);
                DemandHeadBreakdown current = Namuna9Support.ResolveCurrentHeads(demand, calc, prop);
                DemandCollectedBreakdown collected = DemandCollectedBreakdown.FromJson(demand.CollectedByHeadJson);
                decimal noticeAndPenalty = demand.NoticeFee + demand.WarrantFee + demand.PenaltyAmount;
                decimal netPayable = demand.PendingBalance;

                if (ctx.ActiveBillsByProperty.TryGetValue(prop.Id, out PropertyDemandBill? existing))
                {
                    bool matches = DoesBillMatchCurrentDemand(
                        existing,
                        arrears,
                        current,
                        demand,
                        noticeAndPenalty,
                        netPayable);

                    if (matches || !request.RegenerateIfChanged)
                    {
                        reusedRefs.Add(new Namuna9KaGeneratedBillRefDto(
                            BillId: existing.Id,
                            PropertyId: prop.Id,
                            WardNo: prop.WardNo,
                            PropertyNo: prop.PropertyNo,
                            BillNo: existing.BillNo,
                            BillSequence: existing.BillSequence,
                            NetPayableAmount: existing.NetPayableAmount,
                            IssueDate: existing.IssueDate,
                            DueDate: existing.DueDate,
                            UpiPayUri: existing.UpiPayUri,
                            NewlyIssued: false,
                            ReplacedSuperseded: false));
                        continue;
                    }

                    existing.Status = PropertyDemandBill.StatusSuperseded;
                    supersededCount++;
                }

                BillHeadSnapshot snapshot = new(
                    Arrears: arrears,
                    Current: current,
                    Collected: collected,
                    NoticeFee: demand.NoticeFee,
                    WarrantFee: demand.WarrantFee,
                    PenaltyAmount: demand.PenaltyAmount,
                    RebateAmount: demand.RebateAmount,
                    PaidAmount: demand.PaidAmount,
                    NetPayableAmount: netPayable);

                toCreate.Add(new BillGenerationCandidate(
                    Property: prop,
                    Demand: demand,
                    Snapshot: snapshot,
                    NoticeAndPenaltyAmount: noticeAndPenalty,
                    NetPayableAmount: netPayable,
                    ReplacedSuperseded: ctx.ActiveBillsByProperty.ContainsKey(prop.Id)));
            }

            List<PropertyDemandBill> newEntities = new(toCreate.Count);
            List<Namuna9KaGeneratedBillRefDto> createdRefs = new(toCreate.Count);

            if (toCreate.Count > 0)
            {
                long firstSeq = await repository
                    .AllocateBillSequenceRangeAsync(tenantId, ctx.Year.YearLabel, toCreate.Count, ct)
                    .ConfigureAwait(false);

                for (int i = 0; i < toCreate.Count; i++)
                {
                    BillGenerationCandidate item = toCreate[i];
                    long seq = firstSeq + i;
                    string billNo = $"BILL-{ctx.Year.StartYear}-{seq:D4}";

                    string? upiPayUri = item.NetPayableAmount > 0m
                        ? repository.BuildBillUpiPayUri(
                            ctx.UpiId,
                            ctx.PayeeName,
                            item.NetPayableAmount,
                            billNo,
                            item.Property.WardNo,
                            item.Property.PropertyNo)
                        : null;

                    string billStatus = item.NetPayableAmount <= 0m
                        ? PropertyDemandBill.StatusPaid
                        : item.Demand.PaidAmount > 0m
                            ? PropertyDemandBill.StatusPartiallyPaid
                            : PropertyDemandBill.StatusIssued;

                    PropertyDemandBill entity = new()
                    {
                        TenantId = tenantId,
                        PropertyId = item.Property.Id,
                        PropertyDemandId = item.Demand.Id,
                        AssessmentYearId = ctx.Year.Id,
                        BillNo = billNo,
                        BillSequence = seq,
                        IssueDate = issueDate,
                        DueDate = dueDate,
                        RebateCutoffDate = item.Demand.RebateCutoffDate,
                        ArrearsAmount = item.Demand.ArrearsBalance,
                        CurrentAmount = item.Demand.CurrentDemand,
                        NoticeAndPenaltyAmount = item.NoticeAndPenaltyAmount,
                        RebateAmount = item.Demand.RebateAmount,
                        NetPayableAmount = item.NetPayableAmount,
                        HeadBreakdownJson = item.Snapshot.ToJson(),
                        UpiIdUsed = ctx.UpiId,
                        UpiPayUri = upiPayUri,
                        Status = billStatus,
                        IssuedByUserId = userId,
                        IssuedAtUtc = nowUtc,
                    };

                    newEntities.Add(entity);
                    createdRefs.Add(new Namuna9KaGeneratedBillRefDto(
                        BillId: entity.Id,
                        PropertyId: item.Property.Id,
                        WardNo: item.Property.WardNo,
                        PropertyNo: item.Property.PropertyNo,
                        BillNo: billNo,
                        BillSequence: seq,
                        NetPayableAmount: item.NetPayableAmount,
                        IssueDate: issueDate,
                        DueDate: dueDate,
                        UpiPayUri: upiPayUri,
                        NewlyIssued: true,
                        ReplacedSuperseded: item.ReplacedSuperseded));
                }

                await repository.AddBillsAsync(newEntities, ct).ConfigureAwait(false);
            }

            List<Namuna9KaGeneratedBillRefDto> allRefs =
            [
                .. createdRefs.Concat(reusedRefs)
                    .OrderBy(r => r.WardNo, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.PropertyNo, StringComparer.OrdinalIgnoreCase),
            ];

            if (createdRefs.Count > 0 || supersededCount > 0)
            {
                await repository.AddAuditLogsAsync(
                    [
                        Namuna9Support.CreateAudit(
                            currentUserService,
                            dateTimeProvider,
                            tenantId,
                            nameof(PropertyDemandBill),
                            ctx.Year.Id.ToString(),
                            "Namuna9KaBillsGenerated",
                            null,
                            JsonSerializer.Serialize(new
                            {
                                ctx.Year.YearLabel,
                                GeneratedCount = createdRefs.Count,
                                ReusedCount = reusedRefs.Count,
                                SupersededCount = supersededCount,
                                IssueDate = issueDate,
                                DueDate = dueDate,
                                BillNos = createdRefs.Select(r => r.BillNo).Take(25),
                            })),
                    ],
                    ct).ConfigureAwait(false);
            }

            if (createdRefs.Count > 0 || supersededCount > 0)
            {
                await repository.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            bool hasUpi = !string.IsNullOrWhiteSpace(ctx.UpiId);
            bool hasPayable = allRefs.Any(r => r.NetPayableAmount > 0m);
            string? warning = !hasUpi && hasPayable
                ? "Verified Gram Panchayat UPI VPA is not configured on an active BankAccount or GP profile; bills were issued without UPI QR codes."
                : null;

            output = new Namuna9KaGenerateResultDto(
                YearLabel: ctx.Year.YearLabel,
                GeneratedCount: createdRefs.Count,
                ReusedCount: reusedRefs.Count,
                SupersededCount: supersededCount,
                HasUpiConfigured: hasUpi,
                ConfiguredUpiId: ctx.UpiId,
                WarningMessage: warning,
                Bills: allRefs);
        }, cancellationToken).ConfigureAwait(false);

        return output!;
    }

    private static bool DoesBillMatchCurrentDemand(
        PropertyDemandBill existing,
        DemandHeadBreakdown arrears,
        DemandHeadBreakdown current,
        PropertyDemand demand,
        decimal noticeAndPenalty,
        decimal netPayable)
    {
        BillHeadSnapshot? snap = BillHeadSnapshot.FromJson(existing.HeadBreakdownJson);
        if (snap is not null)
        {
            return snap.MatchesAmounts(
                arrears,
                current,
                demand.NoticeFee,
                demand.WarrantFee,
                demand.PenaltyAmount,
                demand.RebateAmount,
                netPayable);
        }

        return existing.ArrearsAmount == demand.ArrearsBalance &&
               existing.CurrentAmount == demand.CurrentDemand &&
               existing.NoticeAndPenaltyAmount == noticeAndPenalty &&
               existing.RebateAmount == demand.RebateAmount &&
               existing.NetPayableAmount == netPayable;
    }

    private sealed record BillGenerationCandidate(
        Property Property,
        PropertyDemand Demand,
        BillHeadSnapshot Snapshot,
        decimal NoticeAndPenaltyAmount,
        decimal NetPayableAmount,
        bool ReplacedSuperseded);
}

