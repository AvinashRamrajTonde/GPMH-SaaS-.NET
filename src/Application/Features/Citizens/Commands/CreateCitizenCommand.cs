using System.Text.RegularExpressions;
using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Domain.Entities.Assessments;
using MediatR;

namespace GPMH.Application.Features.Citizens.Commands;

public sealed record CreateCitizenCommand(
    string? CitizenCode,
    string? WardNo,
    string FullNameMr,
    string FullNameEn,
    string Gender,
    string? MobileNo = null,
    string? AadhaarNumber = null,
    string SocialCategory = "General",
    string ExemptionCategory = "NotExempted",
    bool DpdpConsentAccepted = true) : IRequest<CitizenDto>;

public sealed record UpdateCitizenCommand(
    Guid Id,
    string? WardNo,
    string FullNameMr,
    string FullNameEn,
    string Gender,
    string? MobileNo = null,
    string? AadhaarNumber = null,
    string SocialCategory = "General",
    string ExemptionCategory = "NotExempted",
    bool IsActive = true) : IRequest<CitizenDto>;

internal static class CitizenValidationHelper
{
    public static readonly HashSet<string> AllowedGenders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Male",
        "Female",
        "Other",
    };

    public static string NormalizeDigits(string input) =>
        Regex.Replace(input.Trim(), @"[\s-]+", string.Empty);

    public static bool IsValidAadhaarInput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        string digits = NormalizeDigits(raw);
        return digits.Length == 12 && digits.All(char.IsAsciiDigit);
    }
}

public sealed class CreateCitizenCommandValidator : AbstractValidator<CreateCitizenCommand>
{
    public CreateCitizenCommandValidator()
    {
        RuleFor(x => x.CitizenCode).MaximumLength(40);
        RuleFor(x => x.WardNo).MaximumLength(40);
        RuleFor(x => x.FullNameMr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Gender)
            .NotEmpty()
            .Must(g => g is not null && CitizenValidationHelper.AllowedGenders.Contains(g.Trim()))
            .WithMessage("Gender must be Male, Female, or Other.");
        RuleFor(x => x.MobileNo)
            .Matches(@"^[6-9]\d{9}$")
            .When(x => !string.IsNullOrWhiteSpace(x.MobileNo))
            .WithMessage("Mobile number must be a valid 10-digit Indian mobile number.");
        RuleFor(x => x.AadhaarNumber)
            .Must(CitizenValidationHelper.IsValidAadhaarInput)
            .WithMessage("Aadhaar number must be exactly 12 digits.");
        RuleFor(x => x.SocialCategory).NotEmpty().MaximumLength(24);
        RuleFor(x => x.ExemptionCategory).NotEmpty().MaximumLength(40);
        RuleFor(x => x.DpdpConsentAccepted)
            .Equal(true)
            .WithMessage("DPDP Act 2023 citizen consent acknowledgment is required.");
    }
}

public sealed class UpdateCitizenCommandValidator : AbstractValidator<UpdateCitizenCommand>
{
    public UpdateCitizenCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.WardNo).MaximumLength(40);
        RuleFor(x => x.FullNameMr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Gender)
            .NotEmpty()
            .Must(g => g is not null && CitizenValidationHelper.AllowedGenders.Contains(g.Trim()))
            .WithMessage("Gender must be Male, Female, or Other.");
        RuleFor(x => x.MobileNo)
            .Matches(@"^[6-9]\d{9}$")
            .When(x => !string.IsNullOrWhiteSpace(x.MobileNo))
            .WithMessage("Mobile number must be a valid 10-digit Indian mobile number.");
        RuleFor(x => x.AadhaarNumber)
            .Must(CitizenValidationHelper.IsValidAadhaarInput)
            .WithMessage("Aadhaar number must be exactly 12 digits.");
        RuleFor(x => x.SocialCategory).NotEmpty().MaximumLength(24);
        RuleFor(x => x.ExemptionCategory).NotEmpty().MaximumLength(40);
    }
}

public sealed class CitizenCommandsHandler(
    ICitizenRepository citizenRepository,
    ITenantContext tenantContext,
    IPiiProtectionService piiService)
    : IRequestHandler<CreateCitizenCommand, CitizenDto>,
      IRequestHandler<UpdateCitizenCommand, CitizenDto>
{
    public async Task<CitizenDto> Handle(CreateCitizenCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string code = string.IsNullOrWhiteSpace(request.CitizenCode)
            ? await citizenRepository.GenerateNextCitizenCodeAsync(cancellationToken).ConfigureAwait(false)
            : request.CitizenCode.Trim().ToUpperInvariant();

        if (await citizenRepository.ExistsCitizenCodeAsync(code, null, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.CitizenCode)] = [$"Citizen code '{code}' already exists in this Gram Panchayat."],
            });
        }

        byte[]? aadhaarEncrypted = null;
        string? aadhaarBlindIndex = null;
        string? aadhaarLast4 = null;

        if (!string.IsNullOrWhiteSpace(request.AadhaarNumber))
        {
            string digits = CitizenValidationHelper.NormalizeDigits(request.AadhaarNumber);
            aadhaarBlindIndex = piiService.ComputeBlindIndex(digits);

            if (await citizenRepository.ExistsAadhaarBlindIndexAsync(aadhaarBlindIndex, null, cancellationToken).ConfigureAwait(false))
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.AadhaarNumber)] = ["A citizen with this Aadhaar number is already registered in this Gram Panchayat."],
                });
            }

            aadhaarEncrypted = piiService.EncryptString(digits);
            aadhaarLast4 = digits[^4..];
        }

        Citizen citizen = new()
        {
            TenantId = tenantId,
            CitizenCode = code,
            WardNo = string.IsNullOrWhiteSpace(request.WardNo) ? null : request.WardNo.Trim(),
            FullNameMr = request.FullNameMr.Trim(),
            FullNameEn = request.FullNameEn.Trim(),
            Gender = NormalizeGender(request.Gender),
            MobileNo = string.IsNullOrWhiteSpace(request.MobileNo) ? null : request.MobileNo.Trim(),
            AadhaarEncrypted = aadhaarEncrypted,
            AadhaarBlindIndex = aadhaarBlindIndex,
            AadhaarLast4 = aadhaarLast4,
            SocialCategory = request.SocialCategory.Trim(),
            ExemptionCategory = request.ExemptionCategory.Trim(),
            IsActive = true,
        };

        await citizenRepository.AddAsync(citizen, cancellationToken).ConfigureAwait(false);
        await citizenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(citizen, linkedCount: 0);
    }

    public async Task<CitizenDto> Handle(UpdateCitizenCommand request, CancellationToken cancellationToken)
    {
        if (!tenantContext.CurrentTenantId.HasValue)
        {
            throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        }

        Citizen citizen = await citizenRepository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Citizen), request.Id);

        if (!string.IsNullOrWhiteSpace(request.AadhaarNumber))
        {
            string digits = CitizenValidationHelper.NormalizeDigits(request.AadhaarNumber);
            string blindIndex = piiService.ComputeBlindIndex(digits);

            if (await citizenRepository
                    .ExistsAadhaarBlindIndexAsync(blindIndex, citizen.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.AadhaarNumber)] = ["A citizen with this Aadhaar number is already registered in this Gram Panchayat."],
                });
            }

            citizen.AadhaarEncrypted = piiService.EncryptString(digits);
            citizen.AadhaarBlindIndex = blindIndex;
            citizen.AadhaarLast4 = digits[^4..];
        }

        citizen.WardNo = string.IsNullOrWhiteSpace(request.WardNo) ? null : request.WardNo.Trim();
        citizen.FullNameMr = request.FullNameMr.Trim();
        citizen.FullNameEn = request.FullNameEn.Trim();
        citizen.Gender = NormalizeGender(request.Gender);
        citizen.MobileNo = string.IsNullOrWhiteSpace(request.MobileNo) ? null : request.MobileNo.Trim();
        citizen.SocialCategory = request.SocialCategory.Trim();
        citizen.ExemptionCategory = request.ExemptionCategory.Trim();
        citizen.IsActive = request.IsActive;

        await citizenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int linkedCount = await citizenRepository
            .GetLinkedPropertiesCountAsync(citizen.Id, cancellationToken)
            .ConfigureAwait(false);

        return MapToDto(citizen, linkedCount);
    }

    private static string NormalizeGender(string gender) =>
        gender.Trim().ToLowerInvariant() switch
        {
            "male" => "Male",
            "female" => "Female",
            _ => "Other",
        };

    private static CitizenDto MapToDto(Citizen c, int linkedCount) =>
        new(
            c.Id,
            c.TenantId,
            c.CitizenCode,
            c.WardNo,
            c.FullNameMr,
            c.FullNameEn,
            c.Gender,
            c.MobileNo,
            string.IsNullOrWhiteSpace(c.AadhaarLast4) ? null : $"XXXX-XXXX-{c.AadhaarLast4}",
            c.SocialCategory,
            c.ExemptionCategory,
            c.IsActive,
            linkedCount,
            c.CreatedAtUtc,
            c.UpdatedAtUtc);
}

