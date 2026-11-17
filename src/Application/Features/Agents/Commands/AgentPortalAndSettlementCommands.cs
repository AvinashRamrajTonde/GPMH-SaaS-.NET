using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.Features.Onboarding.Commands;
using GPMH.Application.Features.SuperAdmin.Commands;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Agents.Commands;

// 1. Manual Agent Creation by SuperAdmin (optional AutoApprove)
public sealed record CreateAgentBySuperAdminCommand(
    string FullName,
    string MobileNo,
    string Email,
    string Password,
    string Address,
    string VillageOrCity,
    string District,
    string TalukasCsv,
    string IdProofType,
    string AadhaarNumber,
    string PanNumber,
    string? BankName,
    string? BankBranch,
    string? AccountNumber,
    string? IfscCode,
    string? UpiId,
    decimal ApprovedNewGpRatePct = 15m,
    decimal ApprovedRenewalRatePct = 10m,
    bool AutoApprove = true) : IRequest<SuperAdminAgentSummaryDto>;

public sealed class CreateAgentBySuperAdminCommandValidator : AbstractValidator<CreateAgentBySuperAdminCommand>
{
    public CreateAgentBySuperAdminCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MobileNo).NotEmpty().Matches(@"^[6-9]\d{9}$")
            .WithMessage("Valid 10-digit Indian mobile number is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires a lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires a digit.");
        RuleFor(x => x.Address).NotEmpty().MaximumLength(400);
        RuleFor(x => x.VillageOrCity).NotEmpty().MaximumLength(150);
        RuleFor(x => x.District).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TalukasCsv).NotEmpty().MaximumLength(400);
        RuleFor(x => x.AadhaarNumber).NotEmpty()
            .Must(a => RegisterAgentCommandValidator.DigitsOnly(a).Length == 12)
            .WithMessage("Valid 12-digit Aadhaar number is required.");
        RuleFor(x => x.PanNumber).NotEmpty().Matches(@"^[A-Za-z]{5}\d{4}[A-Za-z]$")
            .WithMessage("Valid 10-character PAN number is required.");
        RuleFor(x => x.IfscCode).Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$")
            .When(x => !string.IsNullOrWhiteSpace(x.IfscCode));
        RuleFor(x => x.ApprovedNewGpRatePct).InclusiveBetween(1m, 50m);
        RuleFor(x => x.ApprovedRenewalRatePct).InclusiveBetween(0m, 50m);
    }
}

public sealed class CreateAgentBySuperAdminCommandHandler(
    IAgentRepository agentRepository,
    IPasswordHasherService passwordHasher,
    IPiiProtectionService piiProtection,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateAgentBySuperAdminCommand, SuperAdminAgentSummaryDto>
{
    public async Task<SuperAdminAgentSummaryDto> Handle(
        CreateAgentBySuperAdminCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");
        DateTimeOffset now = dateTimeProvider.UtcNow;

        (User user, FieldAgent agent, List<ProtectedAsset> assets) = await AgentRegistrationHelper.BuildAgentEntitiesAsync(
            fullName: request.FullName,
            mobileNo: request.MobileNo,
            email: request.Email,
            password: request.Password,
            address: request.Address,
            villageOrCity: request.VillageOrCity,
            district: request.District,
            talukasCsv: request.TalukasCsv,
            idProofType: request.IdProofType,
            aadhaarNumber: request.AadhaarNumber,
            panNumber: request.PanNumber,
            bankName: request.BankName,
            bankBranch: request.BankBranch,
            accountNumber: request.AccountNumber,
            ifscCode: request.IfscCode,
            upiId: request.UpiId,
            requestedCommissionRatePct: request.ApprovedNewGpRatePct,
            approvedNewGpRatePct: request.ApprovedNewGpRatePct,
            approvedRenewalRatePct: request.ApprovedRenewalRatePct,
            autoApprove: request.AutoApprove,
            approvedByUserId: actorId,
            approvedAtUtc: now,
            aadhaarDocument: null,
            panDocument: null,
            agentRepository,
            passwordHasher,
            piiProtection,
            cancellationToken).ConfigureAwait(false);

        await agentRepository.RegisterAgentAsync(user, agent, assets, cancellationToken).ConfigureAwait(false);

        await agentRepository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: null,
                actorUserId: actorId,
                actorRole: currentUserService.Role,
                entityName: nameof(FieldAgent),
                entityId: agent.Id.ToString("D"),
                action: "AgentCreatedBySuperAdmin",
                oldValues: null,
                newValues: new
                {
                    agent.AgentCode,
                    agent.FullName,
                    agent.District,
                    KycStatus = agent.KycStatus.ToString(),
                    agent.ApprovedNewGpRatePct,
                    agent.ApprovedRenewalRatePct,
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);
        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SuperAdminAgentSummaryDto> dir = await agentRepository
            .ListSuperAdminAgentDirectoryAsync(null, null, null, agent.AgentCode, cancellationToken)
            .ConfigureAwait(false);

        return dir.First(a => a.AgentId == agent.Id);
    }
}

// 2. Approved Field Agent registers/assists GP Onboarding
public sealed record RegisterAgentGpCommand(
    string AdminUsername,
    string AdminPassword,
    string? AdminEmail,
    CompleteOnboardingCommand Wizard) : IRequest<OnboardingStatusDto>;

public sealed class RegisterAgentGpCommandValidator : AbstractValidator<RegisterAgentGpCommand>
{
    public RegisterAgentGpCommandValidator()
    {
        RuleFor(x => x.AdminUsername).NotEmpty().MinimumLength(4).MaximumLength(120);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires at least one digit.");
        RuleFor(x => x.AdminEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.AdminEmail));
        RuleFor(x => x.Wizard).NotNull().SetValidator(new CompleteOnboardingCommandValidator());
    }
}

public sealed class RegisterAgentGpCommandHandler(
    IAgentRepository agentRepository,
    IOnboardingRepository onboardingRepository,
    IPasswordHasherService passwordHasher,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterAgentGpCommand, OnboardingStatusDto>
{
    public async Task<OnboardingStatusDto> Handle(RegisterAgentGpCommand request, CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        if (agent.KycStatus != AgentKycStatus.Approved)
        {
            throw new AgentStatusGatedException(
                agent.AgentCode,
                agent.KycStatus,
                agent.StatusReason,
                $"Only KYC-approved agents can register Gram Panchayats (current status: {agent.KycStatus}).");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;

        (OnboardingStatusDto dto, Guid tenantId) = await GpRegistrationHelper.RegisterAsync(
            request.AdminUsername,
            request.AdminPassword,
            request.AdminEmail,
            request.Wizard,
            (agent.Id, agent.AgentCode),
            now,
            onboardingRepository,
            passwordHasher,
            cancellationToken).ConfigureAwait(false);

        await agentRepository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: tenantId,
                actorUserId: userId,
                actorRole: currentUserService.Role,
                entityName: nameof(Tenant),
                entityId: tenantId.ToString("D"),
                action: "AgentRegisteredGp",
                oldValues: null,
                newValues: new
                {
                    agent.AgentCode,
                    dto.LgdCode,
                    dto.PanchayatNameEn,
                    dto.TenantStatus,
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);
        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dto;
    }
}

// 3. Agent Self-Service Profile & Bank/UPI Update
public sealed record UpdateAgentProfileCommand(
    string Email,
    string Address,
    string VillageOrCity,
    string District,
    string TalukasCsv,
    string? BankName,
    string? BankBranch,
    string? AccountNumber,
    string? IfscCode,
    string? UpiId) : IRequest<AgentProfileDto>;

public sealed class UpdateAgentProfileCommandValidator : AbstractValidator<UpdateAgentProfileCommand>
{
    public UpdateAgentProfileCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(400);
        RuleFor(x => x.VillageOrCity).NotEmpty().MaximumLength(150);
        RuleFor(x => x.District).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TalukasCsv).NotEmpty().MaximumLength(400);
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.BankBranch).MaximumLength(150);
        RuleFor(x => x.AccountNumber).MaximumLength(64);
        RuleFor(x => x.IfscCode).Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$")
            .When(x => !string.IsNullOrWhiteSpace(x.IfscCode));
        RuleFor(x => x.UpiId).MaximumLength(120);
    }
}

public sealed class UpdateAgentProfileCommandHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateAgentProfileCommand, AgentProfileDto>
{
    public async Task<AgentProfileDto> Handle(UpdateAgentProfileCommand request, CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        agent.Email = request.Email.Trim();
        agent.Address = request.Address.Trim();
        agent.VillageOrCity = request.VillageOrCity.Trim();
        agent.District = request.District.Trim();
        agent.TalukasCsv = request.TalukasCsv.Trim();
        agent.BankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim();
        agent.BankBranch = string.IsNullOrWhiteSpace(request.BankBranch) ? null : request.BankBranch.Trim();
        agent.AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim();
        agent.IfscCode = string.IsNullOrWhiteSpace(request.IfscCode) ? null : request.IfscCode.Trim().ToUpperInvariant();
        agent.UpiId = string.IsNullOrWhiteSpace(request.UpiId) ? null : request.UpiId.Trim();

        User? linkedUser = await agentRepository.FindUserByIdAsync(agent.UserId, cancellationToken).ConfigureAwait(false);
        if (linkedUser is not null)
        {
            linkedUser.Email = agent.Email;
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        await agentRepository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: null,
                actorUserId: userId,
                actorRole: currentUserService.Role,
                entityName: nameof(FieldAgent),
                entityId: agent.Id.ToString("D"),
                action: "AgentProfileUpdated",
                oldValues: null,
                newValues: new
                {
                    agent.AgentCode,
                    agent.District,
                    agent.TalukasCsv,
                    HasBankAccount = !string.IsNullOrWhiteSpace(agent.AccountNumber),
                    HasUpi = !string.IsNullOrWhiteSpace(agent.UpiId),
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return agent.ToProfileDto();
    }
}

// 4. Agent Password Change + Refresh Token Revocation
public sealed record ChangeAgentPasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Unit>;

public sealed class ChangeAgentPasswordCommandValidator : AbstractValidator<ChangeAgentPasswordCommand>
{
    public ChangeAgentPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires at least one digit.");
    }
}

public sealed class ChangeAgentPasswordCommandHandler(
    IAgentRepository agentRepository,
    IPasswordHasherService passwordHasher,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ChangeAgentPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangeAgentPasswordCommand request, CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        User user = await agentRepository.FindUserByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.CurrentPassword)] = ["Current password is incorrect."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = now;

        await agentRepository
            .RevokeAllUserRefreshTokensAsync(userId, "AgentPasswordChanged", now, cancellationToken)
            .ConfigureAwait(false);

        await agentRepository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: null,
                actorUserId: userId,
                actorRole: currentUserService.Role,
                entityName: nameof(FieldAgent),
                entityId: agent.Id.ToString("D"),
                action: "AgentPasswordChanged",
                oldValues: null,
                newValues: new { agent.AgentCode },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

// 5. Agent Withdrawal Payout Request
public sealed record RequestAgentPayoutCommand(
    decimal PayoutAmount,
    string PaymentMode = "UPI",
    string? Remarks = null) : IRequest<AgentPayoutItemDto>;

public sealed class RequestAgentPayoutCommandValidator : AbstractValidator<RequestAgentPayoutCommand>
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPI", "NEFT_RTGS", "IMPS", "BANK_TRANSFER",
    };

    public RequestAgentPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutAmount).GreaterThan(0m).LessThanOrEqualTo(99_999_999.99m);
        RuleFor(x => x.PaymentMode).NotEmpty().Must(m => AllowedModes.Contains(m.Trim()))
            .WithMessage("PaymentMode must be UPI, NEFT_RTGS, IMPS, or BANK_TRANSFER.");
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public sealed class RequestAgentPayoutCommandHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RequestAgentPayoutCommand, AgentPayoutItemDto>
{
    public async Task<AgentPayoutItemDto> Handle(RequestAgentPayoutCommand request, CancellationToken cancellationToken)
    {
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated Field Agent context is required.");

        FieldAgent agent = await agentRepository.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), userId);

        if (agent.KycStatus != AgentKycStatus.Approved)
        {
            throw new AgentStatusGatedException(
                agent.AgentCode,
                agent.KycStatus,
                agent.StatusReason,
                $"Only KYC-approved agents can request payouts (current status: {agent.KycStatus}).");
        }

        bool hasPayoutChannel = !string.IsNullOrWhiteSpace(agent.UpiId) ||
            (!string.IsNullOrWhiteSpace(agent.BankName) &&
             !string.IsNullOrWhiteSpace(agent.AccountNumber) &&
             !string.IsNullOrWhiteSpace(agent.IfscCode));

        if (!hasPayoutChannel)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["BankDetails"] = ["Please configure your UPI ID or Bank Account details before requesting a payout."],
            });
        }

        decimal requestedAmount = Math.Round(request.PayoutAmount, 2, MidpointRounding.AwayFromZero);
        decimal available = await agentRepository
            .GetAvailableRequestableCommissionAsync(agent.Id, cancellationToken)
            .ConfigureAwait(false);

        if (requestedAmount > available)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.PayoutAmount)] = [$"Requested payout ₹{requestedAmount:0.00} exceeds available requestable balance ₹{available:0.00}."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        int startYear = today.Month >= 4 ? today.Year : today.Year - 1;
        string fy = $"{startYear}-{(startYear + 1) % 100:D2}";
        Guid payoutId = Guid.NewGuid();
        string reqVoucherNo = $"REQ-{now:yyyyMMdd}-{payoutId.ToString("N")[..6].ToUpperInvariant()}";

        AgentPayout payout = new()
        {
            Id = payoutId,
            AgentId = agent.Id,
            VoucherNo = reqVoucherNo,
            VoucherSequence = 0,
            FinancialYear = fy,
            PayoutAmount = requestedAmount,
            PaymentMode = request.PaymentMode.Trim().ToUpperInvariant(),
            UtrReference = "PENDING",
            PaymentDate = today,
            Status = AgentPayout.StatusRequested,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
            RequestedByUserId = userId,
            RequestedAtUtc = now,
        };

        await agentRepository.AddAgentPayoutAsync(payout, cancellationToken).ConfigureAwait(false);
        await agentRepository.AddAuditLogAsync(
            SubscriptionGstHelper.BuildPlatformAudit(
                tenantId: null,
                actorUserId: userId,
                actorRole: currentUserService.Role,
                entityName: nameof(AgentPayout),
                entityId: payout.Id.ToString("D"),
                action: "AgentPayoutRequested",
                oldValues: null,
                newValues: new
                {
                    agent.AgentCode,
                    payout.VoucherNo,
                    payout.PayoutAmount,
                    payout.PaymentMode,
                },
                utcNow: now,
                ipAddress: currentUserService.IpAddress),
            cancellationToken).ConfigureAwait(false);

        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AgentPayoutItemDto(
            PayoutId: payout.Id,
            AgentId: agent.Id,
            AgentCode: agent.AgentCode,
            AgentName: agent.FullName,
            VoucherNo: payout.VoucherNo,
            VoucherSequence: payout.VoucherSequence,
            FinancialYear: payout.FinancialYear,
            PayoutAmount: payout.PayoutAmount,
            PaymentMode: payout.PaymentMode,
            UtrReference: payout.UtrReference,
            PaymentDate: payout.PaymentDate,
            Status: payout.Status,
            ReceiptAssetKey: payout.ReceiptAssetKey,
            Remarks: payout.Remarks,
            RequestedAtUtc: payout.RequestedAtUtc,
            PaidAtUtc: payout.PaidAtUtc,
            CreatedAtUtc: now);
    }
}

// 6. SuperAdmin Partial or Full Agent Commission Settlement Disbursal
public sealed record SettleAgentPayoutCommand(
    Guid AgentId,
    decimal Amount,
    string PaymentMode,
    string UtrReference,
    DateOnly PaymentDate,
    Guid? FulfillRequestedPayoutId = null,
    string? ReceiptAssetKey = null,
    string? Remarks = null) : IRequest<AgentPayoutItemDto>;

public sealed class SettleAgentPayoutCommandValidator : AbstractValidator<SettleAgentPayoutCommand>
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPI", "NEFT_RTGS", "IMPS", "CASH", "BANK_TRANSFER",
    };

    public SettleAgentPayoutCommandValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(99_999_999.99m);
        RuleFor(x => x.PaymentMode).NotEmpty().Must(m => AllowedModes.Contains(m.Trim()))
            .WithMessage("PaymentMode must be UPI, NEFT_RTGS, IMPS, CASH, or BANK_TRANSFER.");
        RuleFor(x => x.UtrReference).NotEmpty().MinimumLength(4).MaximumLength(120);
        RuleFor(x => x.ReceiptAssetKey).MaximumLength(128);
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public sealed class SettleAgentPayoutCommandHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SettleAgentPayoutCommand, AgentPayoutItemDto>
{
    public Task<AgentPayoutItemDto> Handle(SettleAgentPayoutCommand request, CancellationToken cancellationToken)
    {
        Guid actorId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        return agentRepository.SettleAgentPayoutAsync(
            agentId: request.AgentId,
            amount: request.Amount,
            paymentMode: request.PaymentMode,
            utrReference: request.UtrReference,
            paymentDate: request.PaymentDate,
            fulfillRequestedPayoutId: request.FulfillRequestedPayoutId,
            receiptAssetKey: request.ReceiptAssetKey,
            remarks: request.Remarks,
            actorUserId: actorId,
            actorRole: currentUserService.Role,
            ipAddress: currentUserService.IpAddress,
            utcNow: dateTimeProvider.UtcNow,
            cancellationToken: cancellationToken);
    }
}

