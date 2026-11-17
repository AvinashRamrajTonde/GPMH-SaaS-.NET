using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna10.Commands;

public sealed record CollectTaxCommand(
    Guid PropertyId,
    Guid AssessmentYearId,
    Guid OperationId,
    decimal AmountPaid,
    PaymentMode PaymentMode,
    DateOnly? ReceiptDate = null,
    string? BookNo = null,
    string? PayerName = null,
    string? ReferenceNo = null,
    string? BankName = null,
    DateOnly? ChequeDate = null,
    string? Remarks = null,
    bool ApplyEligibleEarlyRebate = false) : IRequest<TaxReceiptDto>;

public sealed class CollectTaxCommandValidator : AbstractValidator<CollectTaxCommand>
{
    public CollectTaxCommandValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.AssessmentYearId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.AmountPaid)
            .GreaterThan(0m)
            .WithMessage("Payment amount must be greater than zero.")
            .LessThanOrEqualTo(99_999_999.99m);
        RuleFor(x => x.PaymentMode).IsInEnum();
        RuleFor(x => x.BookNo).MaximumLength(24);
        RuleFor(x => x.PayerName).MaximumLength(200);
        RuleFor(x => x.ReferenceNo).MaximumLength(120);
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.Remarks).MaximumLength(600);

        When(x => x.PaymentMode == PaymentMode.Cheque, () =>
        {
            RuleFor(x => x.ReferenceNo)
                .NotEmpty()
                .WithMessage("Cheque / DD number is required for Cheque payments.");
            RuleFor(x => x.BankName)
                .NotEmpty()
                .WithMessage("Issuing bank name is required for Cheque payments.");
        });

        When(x => x.PaymentMode is PaymentMode.Upi or PaymentMode.NeftRtgs or PaymentMode.OnlineGateway, () =>
        {
            RuleFor(x => x.ReferenceNo)
                .NotEmpty()
                .WithMessage("UTR / transaction reference number is required for UPI, NEFT/RTGS, and online payments.");
        });
    }
}

public sealed class CollectTaxCommandHandler(
    INamuna10Repository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CollectTaxCommand, TaxReceiptDto>
{
    public async Task<TaxReceiptDto> Handle(
        CollectTaxCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (
            ApplicationRole.SuperAdmin or
            ApplicationRole.Gramsevak or
            ApplicationRole.GP_Admin or
            ApplicationRole.GP_Operator))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak, GP Admin, GP Operator, or SuperAdmin can collect Namuna 10 tax receipts.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");
        Guid collectorUserId = currentUserService.UserId
            ?? throw new TenantAccessDeniedException("Authenticated user context is missing.");

        Namuna10ReceiptContext? replay = await repository
            .FindReceiptByOperationIdAsync(request.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return Namuna10Support.ToReceiptDto(replay, repository, idempotentReplay: true);
        }

        TaxReceiptDto? created = null;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            Namuna10ReceiptContext? insideReplay = await repository
                .FindReceiptByOperationIdAsync(request.OperationId, ct)
                .ConfigureAwait(false);
            if (insideReplay is not null)
            {
                created = Namuna10Support.ToReceiptDto(insideReplay, repository, idempotentReplay: true);
                return;
            }

            Namuna10CollectionContext ctx = await repository
                .LoadCollectionContextAsync(request.PropertyId, request.AssessmentYearId, ct)
                .ConfigureAwait(false)
                ?? throw new NotFoundException(
                    $"Namuna 9 demand for property '{request.PropertyId}' in assessment year '{request.AssessmentYearId}' was not found.");

            Namuna10Support.EnsureNotLocked(ctx.Year);

            PropertyDemand demand = ctx.Demand;
            if (demand.PendingBalance <= 0m)
            {
                throw new DomainConflictException(
                    "PROPERTY_ALREADY_SETTLED",
                    $"Property {ctx.Property.WardNo}/{ctx.Property.PropertyNo} has zero pending dues for FY {ctx.Year.YearLabel}.",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["propertyId"] = ctx.Property.Id,
                        ["assessmentYearId"] = ctx.Year.Id,
                        ["pendingBalance"] = demand.PendingBalance,
                    });
            }

            DateTimeOffset utcNow = dateTimeProvider.UtcNow;
            DateOnly receiptDate = request.ReceiptDate ?? DateOnly.FromDateTime(utcNow.UtcDateTime);

            decimal oldPaidAmount = demand.PaidAmount;
            decimal oldPendingBalance = demand.PendingBalance;
            decimal oldRebateAmount = demand.RebateAmount;
            string oldStatus = demand.Status;

            decimal newlyGrantedRebate = 0m;
            if (request.ApplyEligibleEarlyRebate &&
                !ctx.Property.IsTaxExempt &&
                demand.CurrentDemand > 0m &&
                !demand.EarlyRebateApplied &&
                demand.RebateAmount == 0m)
            {
                decimal rebatePct = ctx.Formula?.EarlyRebatePct ?? Namuna9RebateAndPenaltyCalculator.DefaultEarlyRebatePct;
                DateOnly cutoff = demand.RebateCutoffDate
                    ?? Namuna9RebateAndPenaltyCalculator.DefaultRebateCutoff(ctx.Year.StartYear);

                DemandHeadBreakdown curHeads = Namuna10Support.ResolveCurrentHeads(demand, ctx.TaxCalc, ctx.Property);
                decimal eligibleGeneral = curHeads.GeneralTaxTotal > 0m
                    ? Math.Min(curHeads.GeneralTaxTotal, demand.CurrentDemand)
                    : demand.CurrentDemand;

                decimal calcRebate = Namuna9RebateAndPenaltyCalculator.CalculateEarlyRebate(
                    eligibleGeneral,
                    receiptDate,
                    ctx.Year.StartYear,
                    rebatePct,
                    cutoff);

                calcRebate = Math.Min(calcRebate, demand.CurrentDemand);
                if (calcRebate > 0m)
                {
                    newlyGrantedRebate = calcRebate;
                    demand.RebateAmount = calcRebate;
                    demand.EarlyRebateEligible = true;
                    demand.EarlyRebateApplied = true;
                    demand.RebateCutoffDate = cutoff;
                    demand.RecalculatePendingBalance();
                }
            }

            if (demand.PendingBalance <= 0m)
            {
                demand.Status = PropertyDemand.StatusSettled;
                await repository.SaveChangesAsync(ct).ConfigureAwait(false);
                throw new DomainConflictException(
                    "PROPERTY_ALREADY_SETTLED",
                    "Remaining dues were completely settled by early payment rebate.");
            }

            decimal roundedPaid = Math.Round(request.AmountPaid, 2, MidpointRounding.AwayFromZero);
            Namuna10DueBreakdown dueBreakdown = Namuna10Support.ComputeRemainingDues(demand, ctx.TaxCalc, ctx.Property);
            DemandCollectedBreakdown prevCollected = DemandCollectedBreakdown.FromJson(demand.CollectedByHeadJson);

            Namuna10AllocationOutcome allocation = Namuna10FifoBridge.AllocatePayment(
                amountPaid: roundedPaid,
                startYear: ctx.Year.StartYear,
                yearLabel: ctx.Year.YearLabel,
                dueBreakdown: dueBreakdown,
                previouslyCollected: prevCollected,
                newlyGrantedRebate: newlyGrantedRebate,
                totalDemandRebateAfter: demand.RebateAmount);

            long seq = await repository
                .AllocateNextReceiptSequenceAsync(tenantId, ctx.Year.YearLabel, ct)
                .ConfigureAwait(false);

            string receiptNo = $"RCP-{ctx.Year.StartYear}-{seq:D4}";
            string bookNo = string.IsNullOrWhiteSpace(request.BookNo) ? "BOOK-1" : request.BookNo.Trim();
            string payerName = !string.IsNullOrWhiteSpace(request.PayerName)
                ? request.PayerName.Trim()
                : !string.IsNullOrWhiteSpace(ctx.DefaultOwnerNameMr)
                    ? ctx.DefaultOwnerNameMr
                    : ctx.DefaultOwnerNameEn ?? "Taxpayer";

            TaxReceipt receipt = new()
            {
                TenantId = tenantId,
                PropertyId = ctx.Property.Id,
                PropertyDemandId = demand.Id,
                AssessmentYearId = ctx.Year.Id,
                OperationId = request.OperationId,
                BookNo = bookNo,
                ReceiptNo = receiptNo,
                ReceiptSequence = seq,
                ReceiptDate = receiptDate,
                CollectedAtUtc = utcNow,
                PaymentMode = request.PaymentMode,
                PayerName = payerName,
                ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim(),
                BankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim(),
                ChequeDate = request.ChequeDate,
                Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                TotalPaid = roundedPaid,
                DiscountGiven = newlyGrantedRebate,
                ExcessCredit = allocation.ExcessCredit,
                HeadAllocationJson = allocation.ReceiptAllocation.ToJson(),
                IsCancelled = false,
                CollectedByUserId = collectorUserId,
                DemandBillId = ctx.ActiveBill?.Id,
            };

            demand.CollectedByHeadJson = allocation.UpdatedCollected.ToJson();
            demand.PaidAmount += allocation.AppliedTowardDemand;
            demand.PublishedAtUtc ??= utcNow;
            demand.RecalculatePendingBalance();
            demand.Status = demand.PendingBalance <= 0m
                ? PropertyDemand.StatusSettled
                : PropertyDemand.StatusPartiallyPaid;

            if (ctx.ActiveBill is not null &&
                !string.Equals(ctx.ActiveBill.Status, PropertyDemandBill.StatusSuperseded, StringComparison.OrdinalIgnoreCase))
            {
                ctx.ActiveBill.Status = demand.PendingBalance <= 0m
                    ? PropertyDemandBill.StatusPaid
                    : PropertyDemandBill.StatusPartiallyPaid;
            }

            string oldAuditJson = JsonSerializer.Serialize(new
            {
                PaidAmount = oldPaidAmount,
                PendingBalance = oldPendingBalance,
                RebateAmount = oldRebateAmount,
                Status = oldStatus,
            });
            string newAuditJson = JsonSerializer.Serialize(new
            {
                ReceiptNo = receiptNo,
                BookNo = bookNo,
                PaymentMode = request.PaymentMode.ToString(),
                TotalPaid = roundedPaid,
                AppliedTowardDemand = allocation.AppliedTowardDemand,
                ExcessCredit = allocation.ExcessCredit,
                PaidAmount = demand.PaidAmount,
                PendingBalance = demand.PendingBalance,
                Status = demand.Status,
            });

            await repository.AddReceiptAsync(receipt, ct).ConfigureAwait(false);
            await repository.AddAuditLogAsync(
                Namuna10Support.CreateAudit(
                    tenantId,
                    collectorUserId,
                    currentUserService.Role?.ToString() ?? string.Empty,
                    nameof(TaxReceipt),
                    receipt.Id.ToString("D"),
                    "Namuna10ReceiptIssued",
                    oldAuditJson,
                    newAuditJson,
                    utcNow),
                ct).ConfigureAwait(false);

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);

            created = Namuna10Support.ToReceiptDto(
                new Namuna10ReceiptContext(
                    Receipt: receipt,
                    Year: ctx.Year,
                    Property: ctx.Property,
                    Demand: demand,
                    Bill: ctx.ActiveBill,
                    OwnerNameMr: ctx.DefaultOwnerNameMr,
                    OwnerNameEn: ctx.DefaultOwnerNameEn,
                    OwnerAadhaarMasked: ctx.OwnerAadhaarMasked,
                    OwnerMobileMasked: ctx.OwnerMobileMasked),
                repository,
                idempotentReplay: false);
        }, cancellationToken).ConfigureAwait(false);

        return created!;
    }
}

