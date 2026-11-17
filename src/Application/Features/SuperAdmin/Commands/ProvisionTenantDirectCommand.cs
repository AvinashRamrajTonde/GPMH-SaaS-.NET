using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.SuperAdmin.Commands;

public sealed record ProvisionTenantDirectCommand(
    string LgdCode,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string DistrictName,
    string TehsilName,
    string? DistrictCode,
    string? TehsilCode,
    string? Pincode,
    string? CensusCode,
    string? OfficeAddress,
    string AdminFullName,
    string AdminUsername,
    string AdminEmail,
    string AdminPassword,
    string PlanName = "Standard",
    string PaymentMode = "UPI",
    decimal PaymentAmount = 6000m,
    int DurationDays = 365,
    string? PaymentReference = null,
    DateTimeOffset? PaymentDateUtc = null,
    int TotalWards = 5,
    int? PopulationCensus = null,
    string? ReferralCode = null) : IRequest<ProvisionedTenantResultDto>;

public sealed class ProvisionTenantDirectCommandValidator : AbstractValidator<ProvisionTenantDirectCommand>
{
    private static readonly HashSet<string> AllowedPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        "Standard", "Enterprise",
    };

    private static readonly HashSet<string> AllowedPaymentModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPI", "NEFT_RTGS", "CASH", "CHEQUE", "TREASURY",
    };

    public ProvisionTenantDirectCommandValidator()
    {
        RuleFor(x => x.LgdCode).NotEmpty().Matches(@"^\d{5,8}$");
        RuleFor(x => x.PanchayatNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PanchayatNameMr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DistrictName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TehsilName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AdminFullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminUsername).NotEmpty().MinimumLength(4).MaximumLength(120);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires at least one digit.");
        RuleFor(x => x.PlanName).NotEmpty().Must(p => AllowedPlans.Contains(p.Trim()))
            .WithMessage("PlanName must be Standard or Enterprise.");
        RuleFor(x => x.PaymentMode).NotEmpty().Must(m => AllowedPaymentModes.Contains(m.Trim()))
            .WithMessage("Unsupported offline payment mode.");
        RuleFor(x => x.PaymentAmount).GreaterThanOrEqualTo(1m);
        RuleFor(x => x.DurationDays).InclusiveBetween(30, 3650);
        RuleFor(x => x.TotalWards).InclusiveBetween(1, 50);
    }
}

public sealed class ProvisionTenantDirectCommandHandler(
    ISuperAdminProvisioningRepository repository,
    IPlatformBillingRepository billingRepository,
    IOnboardingRepository onboardingRepository,
    IPasswordHasherService passwordHasher,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IEmailService emailService,
    ISmsService smsService)
    : IRequestHandler<ProvisionTenantDirectCommand, ProvisionedTenantResultDto>
{
    public async Task<ProvisionedTenantResultDto> Handle(
        ProvisionTenantDirectCommand request,
        CancellationToken cancellationToken)
    {
        Guid actorUserId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        string lgdCode = request.LgdCode.Trim();
        string username = request.AdminUsername.Trim();

        if (await repository.ExistsTenantLgdOrUsernameAsync(lgdCode, username, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.LgdCode)] = [$"A tenant with LGD code '{lgdCode}' or admin username '{username}' already exists."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        DateTimeOffset expiry = now.AddDays(request.DurationDays);

        (Guid AgentId, string AgentCode)? refAgent = null;
        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            refAgent = await onboardingRepository
                .FindApprovedAgentByCodeAsync(request.ReferralCode.Trim(), cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.ReferralCode)] = [$"Referral code '{request.ReferralCode.Trim()}' is invalid or belongs to an unapproved agent."],
                });
        }

        Tenant tenant = new()
        {
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = request.PanchayatNameEn.Trim(),
            NameMr = request.PanchayatNameMr.Trim(),
            SubscriptionExpiresAtUtc = expiry,
            ReferredByAgentId = refAgent?.AgentId,
            ReferralCodeUsed = refAgent?.AgentCode,
        };
        tenant.Activate(now);

        TenantDatabaseMapping mapping = new()
        {
            TenantId = tenant.Id,
            IsolationTier = "SHARED",
        };

        GramPanchayat gp = new()
        {
            TenantId = tenant.Id,
            LgdCode = lgdCode,
            DistrictCode = string.IsNullOrWhiteSpace(request.DistrictCode) ? "27" : request.DistrictCode.Trim(),
            DistrictName = request.DistrictName.Trim(),
            TehsilCode = string.IsNullOrWhiteSpace(request.TehsilCode) ? "000" : request.TehsilCode.Trim(),
            TehsilName = request.TehsilName.Trim(),
            PanchayatNameEn = request.PanchayatNameEn.Trim(),
            PanchayatNameMr = request.PanchayatNameMr.Trim(),
            Pincode = request.Pincode?.Trim(),
            CensusCode = request.CensusCode?.Trim(),
            OfficeAddress = request.OfficeAddress?.Trim(),
            TotalWards = request.TotalWards,
            PopulationCensus = request.PopulationCensus,
            GramsevakName = request.AdminFullName.Trim(),
            GramsevakMobile = username,
            SetupProgressPct = 100,
        };

        int startYear = now.Month >= 4 ? now.Year : now.Year - 1;
        AssessmentYear assessmentYear = new()
        {
            TenantId = tenant.Id,
            YearLabel = $"{startYear}-{(startYear + 1) % 100:D2}",
            StartYear = startYear,
            EndYear = startYear + 1,
            IsActive = true,
            IsLocked = false,
        };

        User adminUser = new()
        {
            Username = username,
            Email = request.AdminEmail.Trim(),
            Phone = username,
            FullNameEn = request.AdminFullName.Trim(),
            FullNameMr = request.AdminFullName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.AdminPassword),
            IsSuperAdmin = false,
            IsActive = true,
        };

        TenantMembership membership = new()
        {
            UserId = adminUser.Id,
            TenantId = tenant.Id,
            Role = ApplicationRole.Gramsevak,
            IsDefault = true,
            IsActive = true,
        };

        string paymentRef = string.IsNullOrWhiteSpace(request.PaymentReference)
            ? $"SA-{now:yyyyMMddHHmmss}-{lgdCode}"
            : request.PaymentReference.Trim();

        PlatformPaymentAccount plat = await billingRepository
            .GetOrCreatePlatformAccountAsync(cancellationToken)
            .ConfigureAwait(false);
        string invoiceNo = await billingRepository
            .GenerateNextInvoiceNumberAsync(now.Year, cancellationToken)
            .ConfigureAwait(false);
        decimal gstAmount = SubscriptionGstHelper.ComputeInclusiveGst(request.PaymentAmount, plat.GstPercent);

        TenantSubscriptionPayment payment = new()
        {
            TenantId = tenant.Id,
            PlanName = request.PlanName.Trim(),
            PaymentMode = request.PaymentMode.Trim().ToUpperInvariant(),
            Amount = request.PaymentAmount,
            GstAmount = gstAmount,
            DurationDays = request.DurationDays,
            PaymentReference = paymentRef,
            InvoiceNo = invoiceNo,
            PaymentDateUtc = request.PaymentDateUtc ?? now,
            RecordedByUserId = actorUserId,
            Notes = $"Direct SuperAdmin provisioning ({request.PlanName.Trim()})",
        };

        await repository.ProvisionDirectTenantAsync(
            tenant,
            mapping,
            gp,
            assessmentYear,
            adminUser,
            membership,
            payment,
            cancellationToken).ConfigureAwait(false);

        await emailService.SendAsync(
            request.AdminEmail.Trim(),
            $"e-Grampanchayat ERP Activated — {tenant.NameEn} (LGD {lgdCode})",
            $"<p>Your Gram Panchayat account is ACTIVE. Username: {username}, Ref: {paymentRef}.</p>",
            cancellationToken).ConfigureAwait(false);

        if (username.All(char.IsAsciiDigit) && username.Length == 10)
        {
            await smsService.SendAsync(
                username,
                $"e-Grampanchayat ERP Activated for {tenant.NameEn} (LGD {lgdCode}). Login ID: {username}.",
                cancellationToken).ConfigureAwait(false);
        }

        return new ProvisionedTenantResultDto(
            tenant.Id,
            tenant.TenantCode,
            tenant.LgdCode,
            tenant.NameEn,
            tenant.NameMr,
            tenant.Status.ToString(),
            expiry,
            adminUser.Username,
            ApplicationRole.Gramsevak.ToString(),
            paymentRef,
            request.PaymentAmount,
            FormulaSeeded: true);
    }
}

