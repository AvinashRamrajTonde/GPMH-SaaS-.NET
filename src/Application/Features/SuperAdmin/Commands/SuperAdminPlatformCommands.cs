using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.SuperAdmin.Commands;

internal static class SubscriptionGstHelper
{
    public static decimal ComputeInclusiveGst(decimal totalAmount, decimal gstPercent)
    {
        decimal effectivePct = gstPercent > 0m ? gstPercent : 18.00m;
        return Math.Round(totalAmount - (totalAmount / (1m + (effectivePct / 100m))), 2, MidpointRounding.AwayFromZero);
    }

    public static AuditLog BuildPlatformAudit(
        Guid? tenantId,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string entityName,
        string entityId,
        string action,
        object? oldValues,
        object? newValues,
        DateTimeOffset utcNow,
        string? ipAddress = null) =>
        new()
        {
            TenantId = tenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorId = actorUserId.ToString("D"),
            ActorRole = actorRole?.ToString(),
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = ipAddress,
            TimestampUtc = utcNow,
        };
}

// 1. 15-Minute Audited Support Impersonation
public sealed record SupportImpersonationCommand(
    Guid TargetTenantId,
    string Reason) : IRequest<SupportImpersonationResultDto>;

public sealed class SupportImpersonationCommandValidator : AbstractValidator<SupportImpersonationCommand>
{
    public SupportImpersonationCommandValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("Support impersonation reason must be at least 10 characters for statutory auditability.")
            .MaximumLength(400);
    }
}

public sealed class SupportImpersonationCommandHandler(
    ISuperAdminProvisioningRepository repository,
    IAuthIdentityRepository identityRepository,
    IJwtTokenService jwtTokenService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SupportImpersonationCommand, SupportImpersonationResultDto>
{
    private static readonly TimeSpan ImpersonationDuration = TimeSpan.FromMinutes(15);

    public async Task<SupportImpersonationResultDto> Handle(
        SupportImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.Role is not (ApplicationRole.SuperAdmin or ApplicationRole.Support_Admin))
        {
            throw new TenantAccessDeniedException("Only SuperAdmin or Support_Admin may start support impersonation.");
        }

        if (currentUserService.IsSupportImpersonation)
        {
            throw new TenantAccessDeniedException("Nested impersonation inside an active support session is prohibited.");
        }

        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated operator context is required.");

        User actorUser = await identityRepository
            .FindUserByIdAsync(actorUserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AuthenticationFailedException("Operator user account was not found.");

        if (!actorUser.IsActive)
        {
            throw new AuthenticationFailedException("Operator user account is inactive.");
        }

        Tenant targetTenant = await repository
            .FindActiveTenantForImpersonationAsync(request.TargetTenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TargetTenantId);

        ApplicationRole actorRole = currentUserService.Role!.Value;
        (string jwt, DateTimeOffset expiresAtUtc) = jwtTokenService.CreateImpersonationToken(
            actorUser,
            targetTenant,
            actorRole,
            ImpersonationDuration);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        string reason = request.Reason.Trim();

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: targetTenant.Id,
                actorUserId: actorUserId,
                actorRole: actorRole,
                entityName: nameof(Tenant),
                entityId: targetTenant.Id.ToString("D"),
                action: "SupportImpersonationStarted",
                oldValues: null,
                newValues: new
                {
                    Reason = reason,
                    TargetLgdCode = targetTenant.LgdCode,
                    TargetNameEn = targetTenant.NameEn,
                    ExpiresAtUtc = expiresAtUtc,
                    ReadOnly = true,
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SupportImpersonationResultDto(
            AccessToken: jwt,
            ExpiresAtUtc: expiresAtUtc,
            ExpiresInSeconds: (int)ImpersonationDuration.TotalSeconds,
            TargetTenantId: targetTenant.Id,
            TargetTenantCode: targetTenant.TenantCode,
            TargetLgdCode: targetTenant.LgdCode,
            TargetTenantNameEn: targetTenant.NameEn,
            TargetTenantNameMr: targetTenant.NameMr,
            ReadOnly: true,
            Reason: reason);
    }
}

public sealed record ExitSupportImpersonationCommand(Guid? TargetTenantId = null) : IRequest<Unit>;

public sealed class ExitSupportImpersonationCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ExitSupportImpersonationCommand, Unit>
{
    public async Task<Unit> Handle(ExitSupportImpersonationCommand request, CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.ImpersonatorUserId
            ?? currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated context is required.");
        Guid? targetTenantId = request.TargetTenantId ?? tenantContext.CurrentTenantId;

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: targetTenantId,
                actorUserId: actorUserId,
                actorRole: currentUserService.Role,
                entityName: nameof(Tenant),
                entityId: (targetTenantId ?? Guid.Empty).ToString("D"),
                action: "SupportImpersonationEnded",
                oldValues: null,
                newValues: new { ExitedAtUtc = dateTimeProvider.UtcNow },
                utcNow: dateTimeProvider.UtcNow,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

// 2. Tenant Status / Trial / Direct Renew
public sealed record UpdateTenantStatusCommand(
    Guid TenantId,
    string Action,
    int? TrialDays = null,
    int? DurationDays = null,
    string? Reason = null) : IRequest<SuperAdminTenantSummaryDto>;

public sealed class UpdateTenantStatusCommandValidator : AbstractValidator<UpdateTenantStatusCommand>
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Activate", "Suspend", "GrantTrial", "DirectRenew",
    };

    public UpdateTenantStatusCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => AllowedActions.Contains(a.Trim()))
            .WithMessage("Action must be Activate, Suspend, GrantTrial, or DirectRenew.");
        When(x => x.TrialDays.HasValue, () =>
            RuleFor(x => x.TrialDays!.Value).InclusiveBetween(1, 90));
        When(x => x.DurationDays.HasValue, () =>
            RuleFor(x => x.DurationDays!.Value).InclusiveBetween(1, 3650));
        RuleFor(x => x.Reason).MaximumLength(400);
    }
}

public sealed class UpdateTenantStatusCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateTenantStatusCommand, SuperAdminTenantSummaryDto>
{
    public async Task<SuperAdminTenantSummaryDto> Handle(
        UpdateTenantStatusCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        Tenant tenant = await repository
            .FindTenantByIdAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        if (tenant.IsDeleted)
        {
            throw new DomainConflictException("TENANT_DELETED", "Restore this tenant from Trash before changing status.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        var prevSnapshot = new
        {
            Status = tenant.Status.ToString(),
            tenant.IsTrial,
            tenant.SubscriptionExpiresAtUtc,
        };

        string actionNorm = request.Action.Trim().ToUpperInvariant();
        switch (actionNorm)
        {
            case "ACTIVATE":
                tenant.IsTrial = false;
                tenant.Activate(now);
                tenant.SubscriptionExpiresAtUtc ??= now.AddDays(request.DurationDays ?? 365);
                break;

            case "SUSPEND":
                tenant.Suspend(now);
                break;

            case "GRANTTRIAL":
                int trialDays = request.TrialDays ?? 14;
                tenant.IsTrial = true;
                tenant.Activate(now);
                tenant.SubscriptionExpiresAtUtc = now.AddDays(trialDays);
                break;

            case "DIRECTRENEW":
                int days = request.DurationDays ?? 365;
                DateTimeOffset baseDate = tenant.SubscriptionExpiresAtUtc.HasValue && tenant.SubscriptionExpiresAtUtc.Value > now
                    ? tenant.SubscriptionExpiresAtUtc.Value
                    : now;
                tenant.IsTrial = false;
                tenant.SubscriptionExpiresAtUtc = baseDate.AddDays(days);
                tenant.Activate(now);
                break;

            default:
                throw new DomainValidationException($"Unsupported tenant status action '{request.Action}'.");
        }

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: tenant.Id,
                actorUserId: actorUserId,
                actorRole: currentUserService.Role,
                entityName: nameof(Tenant),
                entityId: tenant.Id.ToString("D"),
                action: "TenantStatusUpdated",
                oldValues: prevSnapshot,
                newValues: new
                {
                    Action = request.Action.Trim(),
                    Status = tenant.Status.ToString(),
                    tenant.IsTrial,
                    tenant.SubscriptionExpiresAtUtc,
                    Reason = request.Reason?.Trim(),
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SuperAdminTenantSummaryDto> list = await repository
            .ListTenantsFilteredAsync(null, null, null, null, includeDeleted: false, cancellationToken)
            .ConfigureAwait(false);

        return list.First(t => t.TenantId == tenant.Id);
    }
}

// 3. Reset GP Admin / Gramsevak Password
public sealed record ResetTenantAdminPasswordCommand(
    Guid TenantId,
    string NewPassword,
    string? TargetUsername = null) : IRequest<ResetTenantAdminPasswordResultDto>;

public sealed class ResetTenantAdminPasswordCommandValidator : AbstractValidator<ResetTenantAdminPasswordCommand>
{
    public ResetTenantAdminPasswordCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires at least one digit.");
        RuleFor(x => x.TargetUsername).MaximumLength(120);
    }
}

public sealed class ResetTenantAdminPasswordCommandHandler(
    ISuperAdminProvisioningRepository repository,
    IPasswordHasherService passwordHasher,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResetTenantAdminPasswordCommand, ResetTenantAdminPasswordResultDto>
{
    public async Task<ResetTenantAdminPasswordResultDto> Handle(
        ResetTenantAdminPasswordCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        Tenant tenant = await repository
            .FindTenantByIdAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        if (tenant.IsDeleted)
        {
            throw new NotFoundException(nameof(Tenant), request.TenantId);
        }

        (User User, TenantMembership Membership) target = await repository
            .FindTenantAdminUserAsync(tenant.Id, request.TargetUsername, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Active GP admin/Gramsevak user for tenant", tenant.Id);

        DateTimeOffset now = dateTimeProvider.UtcNow;
        target.User.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        target.User.UpdatedAtUtc = now;

        await repository
            .RevokeAllUserRefreshTokensAsync(target.User.Id, "SUPERADMIN_PASSWORD_RESET", now, cancellationToken)
            .ConfigureAwait(false);

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: tenant.Id,
                actorUserId: actorUserId,
                actorRole: currentUserService.Role,
                entityName: nameof(User),
                entityId: target.User.Id.ToString("D"),
                action: "TenantAdminPasswordReset",
                oldValues: null,
                newValues: new { Username = target.User.Username, Role = target.Membership.Role.ToString() },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ResetTenantAdminPasswordResultDto(tenant.Id, target.User.Username, now);
    }
}

// 4. Soft Delete, Restore & Permanent Delete Lifecycle
public sealed record SoftDeleteTenantCommand(Guid TenantId) : IRequest<Unit>;

public sealed class SoftDeleteTenantCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SoftDeleteTenantCommand, Unit>
{
    public async Task<Unit> Handle(SoftDeleteTenantCommand request, CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        Tenant tenant = await repository
            .FindTenantByIdAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        if (tenant.IsDeleted)
        {
            throw new DomainConflictException("TENANT_ALREADY_DELETED", "Tenant is already in Trash.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        tenant.SoftDelete(actorUserId, now);

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenant.Id, actorUserId, currentUserService.Role,
                nameof(Tenant), tenant.Id.ToString("D"),
                "TenantSoftDeleted",
                null, new { tenant.LgdCode, tenant.NameEn, tenant.DeletedAtUtc }, now, currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed record RestoreTenantCommand(Guid TenantId) : IRequest<SuperAdminTenantSummaryDto>;

public sealed class RestoreTenantCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RestoreTenantCommand, SuperAdminTenantSummaryDto>
{
    public async Task<SuperAdminTenantSummaryDto> Handle(
        RestoreTenantCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        Tenant tenant = await repository
            .FindTenantByIdAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        if (!tenant.IsDeleted)
        {
            throw new DomainConflictException("TENANT_NOT_IN_TRASH", "Tenant is not soft-deleted.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        tenant.Restore(now);

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenant.Id, actorUserId, currentUserService.Role,
                nameof(Tenant), tenant.Id.ToString("D"),
                "TenantRestored",
                null, new { tenant.LgdCode, tenant.NameEn }, now, currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SuperAdminTenantSummaryDto> list = await repository
            .ListTenantsFilteredAsync(null, null, null, null, includeDeleted: true, cancellationToken)
            .ConfigureAwait(false);

        return list.First(t => t.TenantId == tenant.Id);
    }
}

public sealed record PermanentDeleteTenantCommand(
    Guid TenantId,
    string ConfirmLgdCode) : IRequest<Unit>;

public sealed class PermanentDeleteTenantCommandValidator : AbstractValidator<PermanentDeleteTenantCommand>
{
    public PermanentDeleteTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ConfirmLgdCode).NotEmpty().WithMessage("ConfirmLgdCode is required for permanent tenant purge.");
    }
}

public sealed class PermanentDeleteTenantCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<PermanentDeleteTenantCommand, Unit>
{
    public async Task<Unit> Handle(PermanentDeleteTenantCommand request, CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        Tenant tenant = await repository
            .FindTenantByIdAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        if (!tenant.IsDeleted)
        {
            throw new DomainConflictException(
                "SOFT_DELETE_REQUIRED_FIRST",
                "Move the tenant to Trash via soft delete before permanent purge.");
        }

        if (!string.Equals(request.ConfirmLgdCode.Trim(), tenant.LgdCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.ConfirmLgdCode)] = ["Confirmation LGD code does not match target Gram Panchayat."],
            });
        }

        AuditLog purgeAudit = SubscriptionGstHelper.BuildPlatformAudit(
            tenantId: null,
            actorUserId: actorUserId,
            actorRole: currentUserService.Role,
            entityName: nameof(Tenant),
            entityId: tenant.Id.ToString("D"),
            action: "TenantPermanentlyDeleted",
            oldValues: new { tenant.Id, tenant.LgdCode, tenant.NameEn, tenant.TenantCode },
            newValues: null,
            utcNow: dateTimeProvider.UtcNow,
            ipAddress: currentUserService.IpAddress);

        await repository.PermanentDeleteTenantAsync(tenant, purgeAudit, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

// 5. Approve / Reject Payment Slip Submission (+365 days & GST Invoice)
public sealed record ApproveTenantPaymentCommand(
    Guid SubmissionId,
    int DurationDays = 365,
    string? Notes = null) : IRequest<TenantPaymentSubmissionDto>;

public sealed class ApproveTenantPaymentCommandValidator : AbstractValidator<ApproveTenantPaymentCommand>
{
    public ApproveTenantPaymentCommandValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 3650);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class ApproveTenantPaymentCommandHandler(
    ISuperAdminProvisioningRepository repository,
    IPlatformBillingRepository billingRepository,
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ApproveTenantPaymentCommand, TenantPaymentSubmissionDto>
{
    public async Task<TenantPaymentSubmissionDto> Handle(
        ApproveTenantPaymentCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            TenantPaymentSubmission submission = await repository
                .FindPaymentSubmissionAsync(request.SubmissionId, ct)
                .ConfigureAwait(false)
                ?? throw new NotFoundException(nameof(TenantPaymentSubmission), request.SubmissionId);

            if (!string.Equals(submission.Status, TenantPaymentSubmission.StatusPending, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainConflictException(
                    "PAYMENT_SUBMISSION_NOT_PENDING",
                    $"Submission '{submission.UtrReference}' has already been '{submission.Status}'.");
            }

            if (await billingRepository
                    .ExistsNonRejectedUtrAsync(submission.UtrReference, excludeSubmissionId: submission.Id, ct)
                    .ConfigureAwait(false))
            {
                throw new DomainConflictException(
                    "DUPLICATE_UTR_REFERENCE",
                    $"UTR reference '{submission.UtrReference}' conflicts with another active or approved payment.");
            }

            Tenant tenant = await repository
                .FindTenantByIdAsync(submission.TenantId, ct)
                .ConfigureAwait(false)
                ?? throw new NotFoundException(nameof(Tenant), submission.TenantId);

            if (tenant.IsDeleted)
            {
                throw new DomainConflictException("TENANT_DELETED", "Cannot approve payment for a deleted tenant.");
            }

            DateTimeOffset now = dateTimeProvider.UtcNow;
            PlatformPaymentAccount plat = await billingRepository
                .GetOrCreatePlatformAccountAsync(ct)
                .ConfigureAwait(false);
            string invoiceNo = await billingRepository
                .GenerateNextInvoiceNumberAsync(now.Year, ct)
                .ConfigureAwait(false);
            decimal gstAmount = SubscriptionGstHelper.ComputeInclusiveGst(submission.Amount, plat.GstPercent);

            DateTimeOffset baseDate = tenant.SubscriptionExpiresAtUtc.HasValue && tenant.SubscriptionExpiresAtUtc.Value > now
                ? tenant.SubscriptionExpiresAtUtc.Value
                : now;
            tenant.IsTrial = false;
            tenant.SubscriptionExpiresAtUtc = baseDate.AddDays(request.DurationDays);
            tenant.Activate(now);

            TenantSubscriptionPayment payment = new()
            {
                TenantId = tenant.Id,
                PlanName = submission.PlanName,
                PaymentMode = submission.PaymentMode,
                Amount = submission.Amount,
                GstAmount = gstAmount,
                DurationDays = request.DurationDays,
                PaymentReference = submission.UtrReference,
                InvoiceNo = invoiceNo,
                SubmissionId = submission.Id,
                PaymentDateUtc = now,
                RecordedByUserId = actorUserId,
                Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Approved UTR {submission.UtrReference} ({request.DurationDays} days)"
                    : request.Notes.Trim(),
            };

            submission.Status = TenantPaymentSubmission.StatusApproved;
            submission.ReviewedByUserId = actorUserId;
            submission.ReviewedAtUtc = now;
            submission.ApprovedSubscriptionPaymentId = payment.Id;
            submission.InvoiceNo = invoiceNo;
            submission.UpdatedAtUtc = now;

            await repository.AddSubscriptionPaymentAsync(payment, ct).ConfigureAwait(false);
            await agentRepository.TryAccrueCommissionForPaymentAsync(
                tenant,
                payment,
                actorUserId,
                currentUserService.Role,
                currentUserService.IpAddress,
                now,
                ct).ConfigureAwait(false);
            await repository.AddAuditLogAsync(
                SubscriptionGstHelper.BuildPlatformAudit(
                    tenantId: tenant.Id,
                    actorUserId: actorUserId,
                    actorRole: currentUserService.Role,
                    entityName: nameof(TenantPaymentSubmission),
                    entityId: submission.Id.ToString("D"),
                    action: "SubscriptionPaymentApproved",
                    oldValues: new { Status = TenantPaymentSubmission.StatusPending },
                    newValues: new
                    {
                        Status = TenantPaymentSubmission.StatusApproved,
                        InvoiceNo = invoiceNo,
                        PaymentId = payment.Id,
                        UtrReference = submission.UtrReference,
                        Amount = submission.Amount,
                        DurationDays = request.DurationDays,
                        NewExpiresAtUtc = tenant.SubscriptionExpiresAtUtc,
                        ReferredByAgentId = tenant.ReferredByAgentId,
                    },
                    utcNow: now,
                    ipAddress: currentUserService.IpAddress),
                ct).ConfigureAwait(false);

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<TenantPaymentSubmissionDto> list = await repository
            .ListPaymentSubmissionsAsync(null, null, null, cancellationToken)
            .ConfigureAwait(false);

        return list.First(s => s.SubmissionId == request.SubmissionId);
    }
}

public sealed record RejectTenantPaymentCommand(
    Guid SubmissionId,
    string Reason) : IRequest<TenantPaymentSubmissionDto>;

public sealed class RejectTenantPaymentCommandValidator : AbstractValidator<RejectTenantPaymentCommand>
{
    public RejectTenantPaymentCommandValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500);
    }
}

public sealed class RejectTenantPaymentCommandHandler(
    ISuperAdminProvisioningRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RejectTenantPaymentCommand, TenantPaymentSubmissionDto>
{
    public async Task<TenantPaymentSubmissionDto> Handle(
        RejectTenantPaymentCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        TenantPaymentSubmission submission = await repository
            .FindPaymentSubmissionAsync(request.SubmissionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TenantPaymentSubmission), request.SubmissionId);

        if (!string.Equals(submission.Status, TenantPaymentSubmission.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainConflictException(
                "PAYMENT_SUBMISSION_NOT_PENDING",
                $"Submission '{submission.UtrReference}' has already been '{submission.Status}'.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        string trimmedReason = request.Reason.Trim();
        submission.Status = TenantPaymentSubmission.StatusRejected;
        submission.RejectionReason = trimmedReason;
        submission.ReviewedByUserId = actorUserId;
        submission.ReviewedAtUtc = now;
        submission.UpdatedAtUtc = now;

        await repository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: submission.TenantId,
                actorUserId: actorUserId,
                actorRole: currentUserService.Role,
                entityName: nameof(TenantPaymentSubmission),
                entityId: submission.Id.ToString("D"),
                action: "SubscriptionPaymentRejected",
                oldValues: new { Status = TenantPaymentSubmission.StatusPending },
                newValues: new { Status = TenantPaymentSubmission.StatusRejected, Reason = trimmedReason, submission.UtrReference },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<TenantPaymentSubmissionDto> list = await repository
            .ListPaymentSubmissionsAsync(null, null, null, cancellationToken)
            .ConfigureAwait(false);

        return list.First(s => s.SubmissionId == request.SubmissionId);
    }
}

// 6. Platform Payment Account Settings
public sealed record UpdatePlatformPaymentAccountCommand(
    string AccountName,
    string BankName,
    string BranchName,
    string AccountNumber,
    string IfscCode,
    string UpiId,
    decimal AnnualFee,
    decimal GstPercent,
    string? Gstin,
    string? SupportPhone,
    string? SupportEmail) : IRequest<PlatformPaymentAccountDto>;

public sealed class UpdatePlatformPaymentAccountCommandValidator : AbstractValidator<UpdatePlatformPaymentAccountCommand>
{
    public UpdatePlatformPaymentAccountCommandValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BranchName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IfscCode).NotEmpty().Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$");
        RuleFor(x => x.UpiId).NotEmpty().MaximumLength(120);
        RuleFor(x => x.AnnualFee).GreaterThan(0m).LessThanOrEqualTo(9_999_999m);
        RuleFor(x => x.GstPercent).InclusiveBetween(0m, 50m);
        RuleFor(x => x.Gstin).MaximumLength(32);
        RuleFor(x => x.SupportPhone).MaximumLength(32);
        RuleFor(x => x.SupportEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SupportEmail)).MaximumLength(200);
    }
}

public sealed class UpdatePlatformPaymentAccountCommandHandler(
    IPlatformBillingRepository billingRepository,
    ISuperAdminProvisioningRepository superAdminRepo,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdatePlatformPaymentAccountCommand, PlatformPaymentAccountDto>
{
    public async Task<PlatformPaymentAccountDto> Handle(
        UpdatePlatformPaymentAccountCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        PlatformPaymentAccount updated = await billingRepository.UpdatePlatformAccountAsync(
            request.AccountName,
            request.BankName,
            request.BranchName,
            request.AccountNumber,
            request.IfscCode,
            request.UpiId,
            request.AnnualFee,
            request.GstPercent,
            request.Gstin,
            request.SupportPhone,
            request.SupportEmail,
            cancellationToken).ConfigureAwait(false);

        await superAdminRepo.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: null,
                actorUserId: actorUserId,
                actorRole: currentUserService.Role,
                entityName: nameof(PlatformPaymentAccount),
                entityId: updated.Id.ToString("D"),
                action: "PlatformSettingsUpdated",
                oldValues: null,
                newValues: new { updated.AccountName, updated.UpiId, updated.AnnualFee, updated.GstPercent },
                utcNow: dateTimeProvider.UtcNow,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);
        await superAdminRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PlatformPaymentAccountDto(
            updated.Id,
            updated.AccountName,
            updated.BankName,
            updated.BranchName,
            updated.AccountNumber,
            updated.IfscCode,
            updated.UpiId,
            updated.AnnualFee,
            updated.GstPercent,
            updated.Gstin,
            updated.SupportPhone,
            updated.SupportEmail,
            updated.IsActive,
            updated.UpdatedAtUtc ?? updated.CreatedAtUtc);
    }
}

