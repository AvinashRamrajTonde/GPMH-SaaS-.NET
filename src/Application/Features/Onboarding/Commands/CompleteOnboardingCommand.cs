using System.Text.Json;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Application.Features.Onboarding.Queries;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Onboarding.Commands;

public sealed record BankAccountInput(
    string FundType,
    string BankName,
    string BranchName,
    string AccountNumber,
    string IfscCode,
    string? UpiId,
    string? UpiPayeeName,
    string? QrImageKey,
    decimal OpeningBalance = 0m);

public sealed record CompleteOnboardingCommand(
    string LgdCode,
    string DistrictCode,
    string DistrictName,
    string TehsilCode,
    string TehsilName,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string? Pincode,
    string? CensusCode,
    string? OfficeAddress,
    int TotalWards,
    int? PopulationCensus,
    string? SarpanchName,
    string? SarpanchMobile,
    string? UpSarpanchName,
    string? GramsevakName,
    string? GramsevakMobile,
    string? OperatorName,
    string? OperatorMobile,
    string? GramsevakSignatureKey,
    string? SarpanchSignatureKey,
    string? OperatorSignatureKey,
    string? OfficialStampKey,
    string AssessmentYearLabel,
    int AssessmentStartYear,
    int AssessmentEndYear,
    bool LockAssessmentYear,
    bool InitializeArrearsRollover,
    string? PrimaryUpiId,
    string? UpiPayeeName,
    IReadOnlyList<BankAccountInput> BankAccounts,
    bool SeedDefaultFormulas,
    bool ActivateTenant,
    LgdProvenanceDto? Provenance) : IRequest<OnboardingStatusDto>;

public sealed record RegisterSelfServiceGpCommand(
    string AdminUsername,
    string AdminPassword,
    string? AdminEmail,
    CompleteOnboardingCommand Wizard) : IRequest<OnboardingStatusDto>;

public sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    private static readonly HashSet<string> StatutoryFunds = new(StringComparer.OrdinalIgnoreCase)
    {
        "GRAM_NIDHI", "XV_FINANCE", "WATER_SUPPLY",
    };

    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.LgdCode).NotEmpty().Matches(@"^\d{5,8}$").WithMessage("LGD code must be 5 to 8 digits.");
        RuleFor(x => x.DistrictName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TehsilName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PanchayatNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PanchayatNameMr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalWards).InclusiveBetween(1, 50);
        RuleFor(x => x.AssessmentYearLabel).NotEmpty().Matches(@"^\d{4}-\d{2}$");
        RuleFor(x => x.BankAccounts).NotNull();
        RuleForEach(x => x.BankAccounts).ChildRules(bank =>
        {
            bank.RuleFor(b => b.FundType).NotEmpty().Must(f => StatutoryFunds.Contains(f.Trim()))
                .WithMessage("FundType must be GRAM_NIDHI, XV_FINANCE, or WATER_SUPPLY.");
            bank.RuleFor(b => b.BankName).NotEmpty().MaximumLength(200);
            bank.RuleFor(b => b.BranchName).NotEmpty().MaximumLength(200);
            bank.RuleFor(b => b.AccountNumber).NotEmpty().MaximumLength(64);
            bank.RuleFor(b => b.IfscCode).NotEmpty().Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$");
            bank.RuleFor(b => b.OpeningBalance).GreaterThanOrEqualTo(0m);
        });
    }
}

public sealed class RegisterSelfServiceGpCommandValidator : AbstractValidator<RegisterSelfServiceGpCommand>
{
    public RegisterSelfServiceGpCommandValidator()
    {
        RuleFor(x => x.AdminUsername).NotEmpty().MinimumLength(4).MaximumLength(120);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires at least one digit.");
        RuleFor(x => x.Wizard).NotNull().SetValidator(new CompleteOnboardingCommandValidator());
    }
}

public sealed class CompleteOnboardingCommandHandler(
    IOnboardingRepository onboardingRepository,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    ISender sender)
    : IRequestHandler<CompleteOnboardingCommand, OnboardingStatusDto>
{
    public async Task<OnboardingStatusDto> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        Tenant tenant = await onboardingRepository.GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        string lgdCode = request.LgdCode.Trim();
        if (await onboardingRepository.ExistsTenantWithLgdAsync(lgdCode, tenantId, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.LgdCode)] = [$"Another tenant already owns LGD code '{lgdCode}'."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        tenant.LgdCode = lgdCode;
        tenant.NameEn = request.PanchayatNameEn.Trim();
        tenant.NameMr = request.PanchayatNameMr.Trim();

        GramPanchayat? gp = await onboardingRepository.GetGramPanchayatAsync(tenantId, cancellationToken).ConfigureAwait(false);
        bool isNewGp = gp is null;
        gp ??= new GramPanchayat
        {
            TenantId = tenantId,
            LgdCode = lgdCode,
            DistrictCode = request.DistrictCode.Trim(),
            DistrictName = request.DistrictName.Trim(),
            TehsilCode = request.TehsilCode.Trim(),
            TehsilName = request.TehsilName.Trim(),
            PanchayatNameEn = request.PanchayatNameEn.Trim(),
            PanchayatNameMr = request.PanchayatNameMr.Trim(),
        };

        ApplyWizardFields(gp, request);
        if (isNewGp)
        {
            await onboardingRepository.AddGramPanchayatAsync(gp, cancellationToken).ConfigureAwait(false);
        }

        AssessmentYear? ay = await onboardingRepository.GetActiveAssessmentYearAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (ay is null)
        {
            ay = new AssessmentYear
            {
                TenantId = tenantId,
                YearLabel = request.AssessmentYearLabel.Trim(),
                StartYear = request.AssessmentStartYear,
                EndYear = request.AssessmentEndYear,
                IsActive = true,
                IsLocked = request.LockAssessmentYear,
            };
            await onboardingRepository.AddAssessmentYearAsync(ay, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            ay.YearLabel = request.AssessmentYearLabel.Trim();
            ay.StartYear = request.AssessmentStartYear;
            ay.EndYear = request.AssessmentEndYear;
            ay.IsLocked = request.LockAssessmentYear;
            ay.IsActive = true;
        }

        if (request.BankAccounts.Count > 0)
        {
            List<BankAccount> bankEntities = MapBankAccounts(tenantId, request);
            await onboardingRepository.UpsertBankAccountsAsync(tenantId, bankEntities, cancellationToken).ConfigureAwait(false);
        }

        if (request.ActivateTenant)
        {
            tenant.Activate(now);
            tenant.SubscriptionExpiresAtUtc ??= now.AddDays(365);
            gp.SetupProgressPct = 100;
        }

        await onboardingRepository
            .CompleteTenantOnboardingAsync(tenantId, request.SeedDefaultFormulas, cancellationToken)
            .ConfigureAwait(false);

        return await sender.Send(new GetOnboardingStatusQuery(), cancellationToken).ConfigureAwait(false);
    }

    internal static void ApplyWizardFields(GramPanchayat gp, CompleteOnboardingCommand request)
    {
        gp.LgdCode = request.LgdCode.Trim();
        gp.DistrictCode = string.IsNullOrWhiteSpace(request.DistrictCode) ? "27" : request.DistrictCode.Trim();
        gp.DistrictName = request.DistrictName.Trim();
        gp.TehsilCode = string.IsNullOrWhiteSpace(request.TehsilCode) ? "000" : request.TehsilCode.Trim();
        gp.TehsilName = request.TehsilName.Trim();
        gp.PanchayatNameEn = request.PanchayatNameEn.Trim();
        gp.PanchayatNameMr = request.PanchayatNameMr.Trim();
        gp.Pincode = request.Pincode?.Trim();
        gp.CensusCode = request.CensusCode?.Trim();
        gp.OfficeAddress = request.OfficeAddress?.Trim();
        gp.TotalWards = request.TotalWards;
        gp.PopulationCensus = request.PopulationCensus;

        gp.SarpanchName = request.SarpanchName?.Trim();
        gp.SarpanchMobile = request.SarpanchMobile?.Trim();
        gp.UpSarpanchName = request.UpSarpanchName?.Trim();
        gp.GramsevakName = request.GramsevakName?.Trim();
        gp.GramsevakMobile = request.GramsevakMobile?.Trim();
        gp.OperatorName = request.OperatorName?.Trim();
        gp.OperatorMobile = request.OperatorMobile?.Trim();
        gp.GramsevakSignatureKey = request.GramsevakSignatureKey ?? gp.GramsevakSignatureKey;
        gp.SarpanchSignatureKey = request.SarpanchSignatureKey ?? gp.SarpanchSignatureKey;
        gp.OperatorSignatureKey = request.OperatorSignatureKey ?? gp.OperatorSignatureKey;
        gp.OfficialStampKey = request.OfficialStampKey ?? gp.OfficialStampKey;

        gp.IsArrearsRolloverInitialized = request.InitializeArrearsRollover;
        gp.UpiId = request.PrimaryUpiId?.Trim() ?? request.BankAccounts.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.UpiId))?.UpiId;
        gp.UpiPayeeName = request.UpiPayeeName?.Trim() ?? request.PanchayatNameEn.Trim();

        string? primaryAc = request.BankAccounts.FirstOrDefault()?.AccountNumber;
        if (!string.IsNullOrWhiteSpace(primaryAc) && primaryAc.Length >= 4)
        {
            gp.BankAccountLast4 = primaryAc[^4..];
        }

        if (request.Provenance is not null)
        {
            gp.ProvenanceJson = JsonSerializer.Serialize(request.Provenance);
            if (request.Provenance.EditedFields.Count > 0)
            {
                gp.OverridesJson = JsonSerializer.Serialize(request.Provenance.EditedFields);
            }
        }

        int stepsDone = 1; // Step 1 mandatory
        if (!string.IsNullOrWhiteSpace(gp.GramsevakName) && !string.IsNullOrWhiteSpace(gp.SarpanchName))
        {
            stepsDone++;
        }

        stepsDone++; // Step 3 AY
        if (request.BankAccounts.Count > 0 || !string.IsNullOrWhiteSpace(gp.UpiId))
        {
            stepsDone++;
        }

        if (request.SeedDefaultFormulas && request.ActivateTenant)
        {
            stepsDone++;
        }

        gp.SetupProgressPct = (stepsDone * 100) / 5;
    }

    internal static List<BankAccount> MapBankAccounts(Guid tenantId, CompleteOnboardingCommand request) =>
        request.BankAccounts.Select(b => new BankAccount
        {
            TenantId = tenantId,
            FundType = b.FundType.Trim().ToUpperInvariant(),
            BankName = b.BankName.Trim(),
            BranchName = b.BranchName.Trim(),
            AccountNumber = b.AccountNumber.Trim(),
            IfscCode = b.IfscCode.Trim().ToUpperInvariant(),
            UpiId = b.UpiId?.Trim() ?? request.PrimaryUpiId?.Trim(),
            UpiPayeeName = b.UpiPayeeName?.Trim() ?? request.UpiPayeeName?.Trim() ?? request.PanchayatNameEn.Trim(),
            QrImageKey = b.QrImageKey,
            OpeningBalance = b.OpeningBalance,
            IsDefault = string.Equals(b.FundType.Trim(), "GRAM_NIDHI", StringComparison.OrdinalIgnoreCase),
        }).ToList();
}

public sealed class RegisterSelfServiceGpCommandHandler(
    IOnboardingRepository onboardingRepository,
    IPasswordHasherService passwordHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterSelfServiceGpCommand, OnboardingStatusDto>
{
    public async Task<OnboardingStatusDto> Handle(RegisterSelfServiceGpCommand request, CancellationToken cancellationToken)
    {
        CompleteOnboardingCommand w = request.Wizard;
        string lgdCode = w.LgdCode.Trim();
        string username = request.AdminUsername.Trim();

        if (await onboardingRepository.ExistsTenantWithLgdAsync(lgdCode, null, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(w.LgdCode)] = [$"Gram Panchayat with LGD code '{lgdCode}' is already registered."],
            });
        }

        if (await onboardingRepository.ExistsUsernameAsync(username, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.AdminUsername)] = [$"Username '{username}' is already taken."],
            });
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        Tenant tenant = new()
        {
            TenantCode = $"gp-{lgdCode}",
            LgdCode = lgdCode,
            NameEn = w.PanchayatNameEn.Trim(),
            NameMr = w.PanchayatNameMr.Trim(),
            SubscriptionExpiresAtUtc = now.AddDays(365),
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
            DistrictCode = string.IsNullOrWhiteSpace(w.DistrictCode) ? "27" : w.DistrictCode.Trim(),
            DistrictName = w.DistrictName.Trim(),
            TehsilCode = string.IsNullOrWhiteSpace(w.TehsilCode) ? "000" : w.TehsilCode.Trim(),
            TehsilName = w.TehsilName.Trim(),
            PanchayatNameEn = w.PanchayatNameEn.Trim(),
            PanchayatNameMr = w.PanchayatNameMr.Trim(),
        };
        CompleteOnboardingCommandHandler.ApplyWizardFields(gp, w);
        gp.SetupProgressPct = 100;

        AssessmentYear ay = new()
        {
            TenantId = tenant.Id,
            YearLabel = w.AssessmentYearLabel.Trim(),
            StartYear = w.AssessmentStartYear,
            EndYear = w.AssessmentEndYear,
            IsActive = true,
            IsLocked = w.LockAssessmentYear,
        };

        User adminUser = new()
        {
            Username = username,
            Email = request.AdminEmail?.Trim(),
            Phone = w.GramsevakMobile?.Trim(),
            FullNameEn = string.IsNullOrWhiteSpace(w.GramsevakName) ? username : w.GramsevakName.Trim(),
            FullNameMr = string.IsNullOrWhiteSpace(w.GramsevakName) ? username : w.GramsevakName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.AdminPassword),
        };

        TenantMembership membership = new()
        {
            UserId = adminUser.Id,
            TenantId = tenant.Id,
            Role = ApplicationRole.Gramsevak,
            IsDefault = true,
            IsActive = true,
        };

        List<BankAccount> bankAccounts = CompleteOnboardingCommandHandler.MapBankAccounts(tenant.Id, w);

        await onboardingRepository.RegisterSelfServiceTenantAsync(
            tenant,
            mapping,
            gp,
            ay,
            adminUser,
            membership,
            bankAccounts,
            w.SeedDefaultFormulas,
            cancellationToken).ConfigureAwait(false);

        return new OnboardingStatusDto(
            tenant.Id,
            tenant.Status.ToString(),
            gp.SetupProgressPct,
            gp.LgdCode,
            gp.DistrictCode,
            gp.DistrictName,
            gp.TehsilCode,
            gp.TehsilName,
            gp.PanchayatNameEn,
            gp.PanchayatNameMr,
            gp.Pincode,
            gp.CensusCode,
            gp.OfficeAddress,
            gp.TotalWards,
            gp.PopulationCensus,
            new ExecutiveCommitteeDto(
                gp.SarpanchName,
                gp.SarpanchMobile,
                gp.UpSarpanchName,
                gp.GramsevakName,
                gp.GramsevakMobile,
                gp.OperatorName,
                gp.OperatorMobile,
                gp.GramsevakSignatureKey,
                gp.SarpanchSignatureKey,
                gp.OperatorSignatureKey,
                gp.OfficialStampKey),
            new AssessmentYearSetupDto(
                ay.YearLabel,
                ay.StartYear,
                ay.EndYear,
                ay.IsLocked,
                gp.IsArrearsRolloverInitialized),
            bankAccounts.Select(b => new BankAccountDto(
                b.FundType,
                b.BankName,
                b.BranchName,
                b.AccountNumber,
                b.IfscCode,
                b.UpiId,
                b.UpiPayeeName,
                b.QrImageKey,
                b.OpeningBalance)).ToList(),
            w.SeedDefaultFormulas,
            w.Provenance);
    }
}

