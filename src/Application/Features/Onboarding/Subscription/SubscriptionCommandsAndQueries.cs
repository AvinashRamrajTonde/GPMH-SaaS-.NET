using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Onboarding.Subscription;

public sealed record GetGpSubscriptionStatusQuery : IRequest<GpSubscriptionStatusDto>;

public sealed class GetGpSubscriptionStatusQueryHandler(
    IPlatformBillingRepository billingRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetGpSubscriptionStatusQuery, GpSubscriptionStatusDto>
{
    public Task<GpSubscriptionStatusDto> Handle(
        GetGpSubscriptionStatusQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        return billingRepository.GetGpSubscriptionStatusAsync(tenantId, cancellationToken);
    }
}

public sealed record SubmitTenantPaymentCommand(
    string PlanName,
    string PaymentMode,
    decimal Amount,
    string UtrReference,
    DateOnly PaymentDate,
    string? SlipAssetKey = null,
    string? Notes = null) : IRequest<TenantPaymentSubmissionDto>;

public sealed class SubmitTenantPaymentCommandValidator : AbstractValidator<SubmitTenantPaymentCommand>
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPI", "NEFT", "RTGS", "NEFT_RTGS", "CASH", "BANK_DEPOSIT", "CHEQUE",
    };

    private static readonly HashSet<string> AllowedPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        "Standard", "Enterprise", "Renewal",
    };

    public SubmitTenantPaymentCommandValidator()
    {
        RuleFor(x => x.PlanName)
            .NotEmpty()
            .Must(p => AllowedPlans.Contains(p.Trim()))
            .WithMessage("PlanName must be Standard, Enterprise, or Renewal.");
        RuleFor(x => x.PaymentMode)
            .NotEmpty()
            .Must(m => AllowedModes.Contains(m.Trim()))
            .WithMessage("Unsupported payment mode for subscription submission.");
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(99_999_999.99m);
        RuleFor(x => x.UtrReference).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(x => x.SlipAssetKey).MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class SubmitTenantPaymentCommandHandler(
    IPlatformBillingRepository billingRepository,
    ISuperAdminProvisioningRepository superAdminRepo,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SubmitTenantPaymentCommand, TenantPaymentSubmissionDto>
{
    public async Task<TenantPaymentSubmissionDto> Handle(
        SubmitTenantPaymentCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (
            ApplicationRole.SuperAdmin or
            ApplicationRole.Gramsevak or
            ApplicationRole.GP_Admin))
        {
            throw new TenantAccessDeniedException(
                "Only Gramsevak or GP Admin can submit subscription renewal payments.");
        }

        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");
        Guid actorUserId = currentUserService.UserId
            ?? throw new TenantAccessDeniedException("Authenticated user context is missing.");

        Tenant tenant = await superAdminRepo
            .FindTenantByIdAsync(tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        if (tenant.IsDeleted)
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        string normalizedUtr = request.UtrReference.Trim();
        if (await billingRepository
                .ExistsNonRejectedUtrAsync(normalizedUtr, excludeSubmissionId: null, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new DomainConflictException(
                "DUPLICATE_UTR_REFERENCE",
                $"Payment reference / UTR '{normalizedUtr}' has already been submitted or approved.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["utrReference"] = normalizedUtr,
                });
        }

        string? slipKey = string.IsNullOrWhiteSpace(request.SlipAssetKey)
            ? null
            : request.SlipAssetKey.Trim();

        if (slipKey is not null &&
            !await billingRepository.ExistsSlipAssetInTenantAsync(tenantId, slipKey, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.SlipAssetKey)] = ["Uploaded payment slip asset was not found for this Gram Panchayat."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        TenantPaymentSubmission submission = new()
        {
            TenantId = tenantId,
            SubmittedByUserId = actorUserId,
            PlanName = request.PlanName.Trim(),
            PaymentMode = request.PaymentMode.Trim().ToUpperInvariant(),
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            UtrReference = normalizedUtr,
            PaymentDate = request.PaymentDate,
            SlipAssetKey = slipKey,
            Status = TenantPaymentSubmission.StatusPending,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        AuditLog audit = new()
        {
            TenantId = tenantId,
            EntityName = nameof(TenantPaymentSubmission),
            EntityId = submission.Id.ToString("D"),
            Action = "SubscriptionPaymentSubmitted",
            ActorId = actorUserId.ToString("D"),
            ActorRole = currentUserService.Role?.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                submission.PlanName,
                submission.PaymentMode,
                submission.Amount,
                submission.UtrReference,
                submission.PaymentDate,
            }),
            TimestampUtc = now,
        };

        await billingRepository
            .AddPaymentSubmissionAsync(submission, audit, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TenantPaymentSubmissionDto> rows = await superAdminRepo
            .ListPaymentSubmissionsAsync(status: null, tenantId: tenantId, search: null, cancellationToken)
            .ConfigureAwait(false);

        return rows.First(r => r.SubmissionId == submission.Id);
    }
}

public sealed record GetTenantSubscriptionInvoicePdfQuery(Guid PaymentOrSubmissionId)
    : IRequest<GeneratedPdfFileDto>;

public sealed class GetTenantSubscriptionInvoicePdfQueryHandler(
    IPlatformBillingRepository billingRepository,
    IStatutoryPdfRenderer pdfRenderer,
    ITenantContext tenantContext)
    : IRequestHandler<GetTenantSubscriptionInvoicePdfQuery, GeneratedPdfFileDto>
{
    public async Task<GeneratedPdfFileDto> Handle(
        GetTenantSubscriptionInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Tenant context is missing.");

        GstSubscriptionInvoicePdfModel model = await billingRepository
            .BuildGstInvoiceModelAsync(request.PaymentOrSubmissionId, restrictToTenantId: tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TenantSubscriptionPayment), request.PaymentOrSubmissionId);

        byte[] bytes = pdfRenderer.RenderSubscriptionGstInvoicePdf(model);
        string safeInv = model.InvoiceNo.Replace('/', '-').Replace(' ', '_');
        return new GeneratedPdfFileDto($"Invoice_{safeInv}.pdf", bytes);
    }
}

