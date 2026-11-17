using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Namuna10;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Enums;
using GPMH.Domain.Services;
using GPMH.Domain.ValueObjects;
using MediatR;

namespace GPMH.Application.Features.Namuna10.Commands;

public sealed record CancelTaxReceiptCommand(
    Guid ReceiptId,
    string ConfirmPassword,
    string CancelReason) : IRequest<TaxReceiptDto>;

public sealed class CancelTaxReceiptCommandValidator : AbstractValidator<CancelTaxReceiptCommand>
{
    public CancelTaxReceiptCommandValidator()
    {
        RuleFor(x => x.ReceiptId).NotEmpty();
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Gramsevak confirmation password is required to cancel a statutory tax receipt.");
        RuleFor(x => x.CancelReason)
            .NotEmpty()
            .MinimumLength(5)
            .WithMessage("Cancellation reason must be at least 5 characters.")
            .MaximumLength(500);
    }
}

public sealed class CancelTaxReceiptCommandHandler(
    INamuna10Repository repository,
    IAuthIdentityRepository authIdentityRepository,
    IPasswordHasherService passwordHasher,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CancelTaxReceiptCommand, TaxReceiptDto>
{
    public async Task<TaxReceiptDto> Handle(
        CancelTaxReceiptCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Gramsevak))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak or SuperAdmin can cancel statutory Namuna 10 tax receipts.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");
        Guid actorUserId = currentUserService.UserId
            ?? throw new TenantAccessDeniedException("Authenticated user context is missing.");

        User? actorUser = await authIdentityRepository
            .FindUserByIdAsync(actorUserId, cancellationToken)
            .ConfigureAwait(false);

        if (actorUser is null ||
            !actorUser.IsActive ||
            !passwordHasher.VerifyPassword(actorUser.PasswordHash, request.ConfirmPassword))
        {
            throw new AuthenticationFailedException(
                "Invalid confirmation password for statutory receipt cancellation (INVALID_AUTHORIZATION_PASSWORD).");
        }

        TaxReceiptDto? cancelledDto = null;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            Namuna10ReceiptCancelContext ctx = await repository
                .LoadReceiptForCancelAsync(request.ReceiptId, ct)
                .ConfigureAwait(false)
                ?? throw new NotFoundException(nameof(TaxReceipt), request.ReceiptId);

            if (ctx.Receipt.IsCancelled)
            {
                throw new DomainConflictException(
                    "RECEIPT_ALREADY_CANCELLED",
                    $"Tax receipt '{ctx.Receipt.ReceiptNo}' has already been cancelled.",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["receiptId"] = ctx.Receipt.Id,
                        ["receiptNo"] = ctx.Receipt.ReceiptNo,
                        ["cancelledAtUtc"] = ctx.Receipt.CancelledAtUtc,
                    });
            }

            Namuna10Support.EnsureNotLocked(ctx.Year);

            TaxReceipt receipt = ctx.Receipt;
            PropertyDemand demand = ctx.Demand;

            decimal oldPaidAmount = demand.PaidAmount;
            decimal oldPendingBalance = demand.PendingBalance;
            decimal oldRebateAmount = demand.RebateAmount;
            string oldStatus = demand.Status;

            TaxReceiptHeadAllocation alloc = TaxReceiptHeadAllocation.FromJson(receipt.HeadAllocationJson);
            DemandCollectedBreakdown existingCollected = DemandCollectedBreakdown.FromJson(demand.CollectedByHeadJson);
            DemandCollectedBreakdown reversedCollected = Namuna10FifoBridge.ReverseAllocation(
                existingCollected,
                alloc,
                receipt.DiscountGiven);

            demand.CollectedByHeadJson = reversedCollected.ToJson();

            if (receipt.DiscountGiven > 0m)
            {
                demand.RebateAmount = Math.Max(
                    0m,
                    Math.Round(demand.RebateAmount - receipt.DiscountGiven, 2, MidpointRounding.AwayFromZero));

                if (demand.RebateAmount == 0m)
                {
                    demand.EarlyRebateApplied = false;
                }
            }

            decimal allocatedTowardDemand = alloc.TotalAllocatedTowardDemand;
            demand.PaidAmount = Math.Max(
                0m,
                Math.Round(demand.PaidAmount - allocatedTowardDemand, 2, MidpointRounding.AwayFromZero));
            demand.RecalculatePendingBalance();
            demand.Status = demand.PendingBalance <= 0m
                ? PropertyDemand.StatusSettled
                : demand.PaidAmount > 0m
                    ? PropertyDemand.StatusPartiallyPaid
                    : PropertyDemand.StatusPublished;

            if (ctx.Bill is not null &&
                !string.Equals(ctx.Bill.Status, PropertyDemandBill.StatusSuperseded, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Bill.Status = demand.PendingBalance <= 0m
                    ? PropertyDemandBill.StatusPaid
                    : demand.PaidAmount > 0m
                        ? PropertyDemandBill.StatusPartiallyPaid
                        : PropertyDemandBill.StatusIssued;
            }

            DateTimeOffset utcNow = dateTimeProvider.UtcNow;
            string trimmedReason = request.CancelReason.Trim();

            receipt.IsCancelled = true;
            receipt.CancelReason = trimmedReason;
            receipt.CancelledByUserId = actorUserId;
            receipt.CancelledAtUtc = utcNow;

            string oldAuditJson = JsonSerializer.Serialize(new
            {
                IsCancelled = false,
                PaidAmount = oldPaidAmount,
                PendingBalance = oldPendingBalance,
                RebateAmount = oldRebateAmount,
                Status = oldStatus,
            });
            string newAuditJson = JsonSerializer.Serialize(new
            {
                IsCancelled = true,
                ReceiptNo = receipt.ReceiptNo,
                CancelReason = trimmedReason,
                CancelledByUserId = actorUserId,
                CancelledAtUtc = utcNow,
                ReversedAmountTowardDemand = allocatedTowardDemand,
                PaidAmount = demand.PaidAmount,
                PendingBalance = demand.PendingBalance,
                Status = demand.Status,
                BillStatus = ctx.Bill?.Status,
            });

            await repository.AddAuditLogAsync(
                Namuna10Support.CreateAudit(
                    tenantId,
                    actorUserId,
                    currentUserService.Role?.ToString() ?? string.Empty,
                    nameof(TaxReceipt),
                    receipt.Id.ToString("D"),
                    "Namuna10ReceiptCancelled",
                    oldAuditJson,
                    newAuditJson,
                    utcNow),
                ct).ConfigureAwait(false);

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);

            cancelledDto = Namuna10Support.ToReceiptDto(
                new Namuna10ReceiptContext(
                    Receipt: receipt,
                    Year: ctx.Year,
                    Property: ctx.Property,
                    Demand: demand,
                    Bill: ctx.Bill,
                    OwnerNameMr: ctx.OwnerNameMr,
                    OwnerNameEn: ctx.OwnerNameEn,
                    OwnerAadhaarMasked: ctx.OwnerAadhaarMasked,
                    OwnerMobileMasked: ctx.OwnerMobileMasked),
                repository);
        }, cancellationToken).ConfigureAwait(false);

        return cancelledDto!;
    }
}

