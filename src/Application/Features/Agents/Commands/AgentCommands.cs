using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Auth;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Agents.Commands;

public sealed record KycFileUploadInput(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record RegisterAgentCommand(
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
    decimal RequestedCommissionRatePct = 15m,
    KycFileUploadInput? AadhaarDocument = null,
    KycFileUploadInput? PanDocument = null) : IRequest<AgentRegistrationResultDto>;

public sealed record AgentLoginCommand(
    string Identifier,
    string Password) : IRequest<AuthTokenBundleDto>;

public sealed record UpdateAgentStatusCommand(
    Guid AgentId,
    AgentKycStatus Status,
    decimal? ApprovedNewGpRatePct,
    decimal? ApprovedRenewalRatePct,
    string? Reason) : IRequest<AgentProfileDto>;

public sealed class RegisterAgentCommandValidator : AbstractValidator<RegisterAgentCommand>
{
    public RegisterAgentCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MobileNo).NotEmpty().Matches(@"^[6-9]\d{9}$").WithMessage("Valid 10-digit Indian mobile number is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password requires an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password requires a lowercase letter.")
            .Matches(@"\d").WithMessage("Password requires a digit.");
        RuleFor(x => x.Address).NotEmpty().MaximumLength(400);
        RuleFor(x => x.VillageOrCity).NotEmpty().MaximumLength(150);
        RuleFor(x => x.District).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TalukasCsv).NotEmpty().MaximumLength(400);
        RuleFor(x => x.AadhaarNumber).NotEmpty().Must(a => DigitsOnly(a).Length == 12)
            .WithMessage("Valid 12-digit Aadhaar number is required.");
        RuleFor(x => x.PanNumber).NotEmpty().Matches(@"^[A-Za-z]{5}\d{4}[A-Za-z]$")
            .WithMessage("Valid 10-character PAN number (e.g. ABCDE1234F) is required.");
        RuleFor(x => x.IfscCode).Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$")
            .When(x => !string.IsNullOrWhiteSpace(x.IfscCode));
        RuleFor(x => x.RequestedCommissionRatePct).InclusiveBetween(1m, 50m);
        RuleFor(x => x.AadhaarDocument).Must(IsValidKycFile).When(x => x.AadhaarDocument is not null)
            .WithMessage("Aadhaar document must be PDF/JPEG/PNG under 5 MB.");
        RuleFor(x => x.PanDocument).Must(IsValidKycFile).When(x => x.PanDocument is not null)
            .WithMessage("PAN document must be PDF/JPEG/PNG under 5 MB.");
    }

    internal static string DigitsOnly(string raw) =>
        new([.. (raw ?? string.Empty).Where(char.IsAsciiDigit)]);

    private static bool IsValidKycFile(KycFileUploadInput? file)
    {
        if (file is null || file.Content.Length == 0 || file.Content.Length > 5 * 1024 * 1024)
        {
            return false;
        }

        string mime = file.ContentType.Trim().ToLowerInvariant();
        if (mime is not ("application/pdf" or "image/png" or "image/jpeg"))
        {
            return false;
        }

        byte[] b = file.Content;
        bool isPdf = b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;
        bool isPng = b.Length >= 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
        bool isJpeg = b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
        return isPdf || isPng || isJpeg;
    }
}

public sealed class RegisterAgentCommandHandler(
    IAgentRepository agentRepository,
    IPasswordHasherService passwordHasher,
    IPiiProtectionService piiProtection,
    ISmsService smsService)
    : IRequestHandler<RegisterAgentCommand, AgentRegistrationResultDto>
{
    public async Task<AgentRegistrationResultDto> Handle(RegisterAgentCommand request, CancellationToken cancellationToken)
    {
        string mobile = request.MobileNo.Trim();
        string aadhaarDigits = RegisterAgentCommandValidator.DigitsOnly(request.AadhaarNumber);
        string panUpper = request.PanNumber.Trim().ToUpperInvariant();

        string aadhaarBlindIndex = piiProtection.ComputeBlindIndex($"AADHAAR:{aadhaarDigits}");
        string panBlindIndex = piiProtection.ComputeBlindIndex($"PAN:{panUpper}");

        if (await agentRepository.ExistsDuplicateAsync(mobile, aadhaarBlindIndex, panBlindIndex, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.MobileNo)] = ["An agent with the same mobile number, Aadhaar, or PAN is already registered."],
            });
        }

        User user = new()
        {
            Username = mobile,
            Email = request.Email.Trim(),
            Phone = mobile,
            FullNameEn = request.FullName.Trim(),
            FullNameMr = request.FullName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            IsSuperAdmin = false,
            IsActive = true,
        };

        string agentCode = $"AGT-MH-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        List<ProtectedAsset> assets = [];

        string? aadhaarDocKey = null;
        if (request.AadhaarDocument is not null)
        {
            aadhaarDocKey = $"kyc-{user.Id:N}-aadhaar-{Guid.NewGuid():N}";
            assets.Add(new ProtectedAsset
            {
                AssetKey = aadhaarDocKey,
                TenantId = null,
                OwnerUserId = user.Id,
                Category = "KYC_DOC",
                FileName = Path.GetFileName(request.AadhaarDocument.FileName),
                ContentType = request.AadhaarDocument.ContentType.Trim().ToLowerInvariant(),
                EncryptedContent = piiProtection.EncryptBytes(request.AadhaarDocument.Content),
                SizeBytes = request.AadhaarDocument.Content.LongLength,
            });
        }

        string? panDocKey = null;
        if (request.PanDocument is not null)
        {
            panDocKey = $"kyc-{user.Id:N}-pan-{Guid.NewGuid():N}";
            assets.Add(new ProtectedAsset
            {
                AssetKey = panDocKey,
                TenantId = null,
                OwnerUserId = user.Id,
                Category = "KYC_DOC",
                FileName = Path.GetFileName(request.PanDocument.FileName),
                ContentType = request.PanDocument.ContentType.Trim().ToLowerInvariant(),
                EncryptedContent = piiProtection.EncryptBytes(request.PanDocument.Content),
                SizeBytes = request.PanDocument.Content.LongLength,
            });
        }

        FieldAgent agent = new()
        {
            UserId = user.Id,
            AgentCode = agentCode,
            KycStatus = AgentKycStatus.PendingApproval,
            FullName = request.FullName.Trim(),
            MobileNo = mobile,
            Email = request.Email.Trim(),
            IdProofType = string.IsNullOrWhiteSpace(request.IdProofType) ? "AADHAAR" : request.IdProofType.Trim().ToUpperInvariant(),
            PanMasked = piiProtection.MaskPan(panUpper),
            AadhaarMasked = piiProtection.MaskAadhaar(aadhaarDigits),
            AadhaarBlindIndex = aadhaarBlindIndex,
            AadhaarEncrypted = piiProtection.EncryptString(aadhaarDigits),
            PanBlindIndex = panBlindIndex,
            PanEncrypted = piiProtection.EncryptString(panUpper),
            Address = request.Address.Trim(),
            VillageOrCity = request.VillageOrCity.Trim(),
            District = request.District.Trim(),
            TalukasCsv = request.TalukasCsv.Trim(),
            BankName = request.BankName?.Trim(),
            BankBranch = request.BankBranch?.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            IfscCode = request.IfscCode?.Trim().ToUpperInvariant(),
            UpiId = request.UpiId?.Trim(),
            AadhaarDocumentKey = aadhaarDocKey,
            PanDocumentKey = panDocKey,
            RequestedCommissionRatePct = request.RequestedCommissionRatePct,
            ApprovedNewGpRatePct = request.RequestedCommissionRatePct,
            ApprovedRenewalRatePct = Math.Max(5m, request.RequestedCommissionRatePct - 5m),
        };

        await agentRepository.RegisterAgentAsync(user, agent, assets, cancellationToken).ConfigureAwait(false);

        await smsService.SendAsync(
            mobile,
            $"Application {agentCode} submitted and is Pending Approval.",
            cancellationToken).ConfigureAwait(false);

        return new AgentRegistrationResultDto(
            agent.Id,
            user.Id,
            agent.AgentCode,
            agent.FullName,
            agent.MobileNo,
            agent.KycStatus,
            agent.AadhaarMasked,
            agent.PanMasked,
            agent.RequestedCommissionRatePct);
    }
}

public sealed class AgentLoginCommandHandler(
    IAgentRepository agentRepository,
    IAuthIdentityRepository authRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AgentLoginCommand, AuthTokenBundleDto>
{
    public async Task<AuthTokenBundleDto> Handle(AgentLoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AuthenticationFailedException("Mobile number and password are required.");
        }

        string loginId = request.Identifier.Trim();
        User? user = await agentRepository.FindUserByLoginAsync(loginId, cancellationToken).ConfigureAwait(false);

        if (user is null || !user.IsActive || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException("Invalid credentials.");
        }

        FieldAgent? agent = await agentRepository.FindByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            throw new AuthenticationFailedException("User is not registered as a Field Agent.");
        }

        if (agent.KycStatus != AgentKycStatus.Approved)
        {
            throw new AgentStatusGatedException(
                agent.AgentCode,
                agent.KycStatus,
                agent.StatusReason,
                $"Agent account '{agent.AgentCode}' is currently '{agent.KycStatus}' and awaiting SuperAdmin approval.");
        }

        DateTimeOffset now = dateTimeProvider.UtcNow;
        (string accessToken, DateTimeOffset expiresAtUtc) = jwtTokenService.CreateAccessToken(
            user,
            ApplicationRole.Field_Agent,
            tenantId: null,
            agentId: agent.Id);

        (string rawRefreshToken, string refreshHash) = jwtTokenService.CreateRefreshToken();
        RefreshToken refreshEntity = new()
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = now.AddDays(7),
        };

        await authRepository.AddRefreshTokenAsync(refreshEntity, cancellationToken).ConfigureAwait(false);
        await authRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AuthenticatedUserDto userDto = new(
            user.Id,
            user.Username,
            user.FullNameEn,
            user.FullNameMr,
            ApplicationRole.Field_Agent,
            ActiveTenantId: null,
            Memberships: []);

        return new AuthTokenBundleDto(accessToken, rawRefreshToken, expiresAtUtc, userDto);
    }
}

public sealed class UpdateAgentStatusCommandHandler(
    IAgentRepository agentRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IEmailService emailService,
    ISmsService smsService)
    : IRequestHandler<UpdateAgentStatusCommand, AgentProfileDto>
{
    public async Task<AgentProfileDto> Handle(UpdateAgentStatusCommand request, CancellationToken cancellationToken)
    {
        Guid actorId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated SuperAdmin context is required.");

        FieldAgent agent = await agentRepository.FindByIdAsync(request.AgentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FieldAgent), request.AgentId);

        agent.KycStatus = request.Status;
        agent.StatusReason = request.Reason?.Trim();

        if (request.ApprovedNewGpRatePct.HasValue)
        {
            agent.ApprovedNewGpRatePct = request.ApprovedNewGpRatePct.Value;
        }

        if (request.ApprovedRenewalRatePct.HasValue)
        {
            agent.ApprovedRenewalRatePct = request.ApprovedRenewalRatePct.Value;
        }

        if (request.Status == AgentKycStatus.Approved)
        {
            agent.ApprovedByUserId = actorId;
            agent.ApprovedAtUtc = dateTimeProvider.UtcNow;
        }

        await agentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await emailService.SendAsync(
            agent.Email,
            $"Field Agent KYC Status Updated: {agent.KycStatus}",
            $"<p>Agent {agent.AgentCode} status updated to {agent.KycStatus}.</p>",
            cancellationToken).ConfigureAwait(false);

        await smsService.SendAsync(
            agent.MobileNo,
            $"GPMH Agent {agent.AgentCode} status: {agent.KycStatus}.",
            cancellationToken).ConfigureAwait(false);

        return agent.ToProfileDto();
    }
}

internal static class AgentMappingExtensions
{
    public static AgentProfileDto ToProfileDto(this FieldAgent agent) =>
        new(
            agent.Id,
            agent.UserId,
            agent.AgentCode,
            agent.KycStatus,
            agent.StatusReason,
            agent.FullName,
            agent.MobileNo,
            agent.Email,
            agent.District,
            agent.TalukasCsv,
            agent.AadhaarMasked,
            agent.PanMasked,
            agent.RequestedCommissionRatePct,
            agent.ApprovedNewGpRatePct,
            agent.ApprovedRenewalRatePct,
            agent.AadhaarDocumentKey,
            agent.PanDocumentKey);
}

