using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Onboarding;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using MediatR;

namespace GPMH.Application.Features.Onboarding.Commands;

public sealed record UploadProtectedAssetCommand(
    string Category,
    string FileName,
    string ContentType,
    byte[] Content) : IRequest<UploadedAssetDto>;

public sealed record GetProtectedAssetQuery(string AssetKey) : IRequest<ProtectedAssetDownloadDto>;

public sealed class UploadProtectedAssetCommandValidator : AbstractValidator<UploadProtectedAssetCommand>
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "SIGNATURE", "STAMP", "QR", "MUTATION_DOC", "MUTATIONDOC", "PARTITION_DEED", "PAYMENT_SLIP", "SUBSCRIPTION_SLIP",
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf",
    };

    public UploadProtectedAssetCommandValidator()
    {
        RuleFor(x => x.Category).NotEmpty().Must(c => AllowedCategories.Contains(c.Trim()))
            .WithMessage("Category must be SIGNATURE, STAMP, QR, MUTATION_DOC, PARTITION_DEED, or PAYMENT_SLIP.");
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().Must(ct => AllowedMimeTypes.Contains(ct.Trim()))
            .WithMessage("Only PNG, JPEG, WebP images or PDF documents are permitted.");
        RuleFor(x => x.Content).NotNull().Must(b => b.Length > 0 && b.Length <= 5 * 1024 * 1024)
            .WithMessage("Asset file size must be between 1 byte and 5 MB.");
    }
}

public sealed class UploadProtectedAssetCommandHandler(
    IOnboardingRepository onboardingRepository,
    IPiiProtectionService piiProtectionService,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UploadProtectedAssetCommand, UploadedAssetDto>
{
    public async Task<UploadedAssetDto> Handle(UploadProtectedAssetCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");
        Guid userId = currentUserService.UserId
            ?? throw new AuthenticationFailedException("Authenticated user context is required.");

        string safeExtension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (safeExtension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".pdf")
            || !HasValidAssetSignature(safeExtension, request.Content))
        {
            throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(request.Content)] = ["Uploaded file bytes do not match PNG, JPEG, WebP, or PDF magic headers."],
            });
        }

        string category = request.Category.Trim().ToUpperInvariant();
        string assetKey = $"gp-{tenantId:N}-{category.ToLowerInvariant()}-{Guid.NewGuid():N}";
        byte[] encrypted = piiProtectionService.EncryptBytes(request.Content);

        ProtectedAsset asset = new()
        {
            AssetKey = assetKey,
            TenantId = tenantId,
            OwnerUserId = userId,
            Category = category,
            FileName = Path.GetFileName(request.FileName),
            ContentType = request.ContentType.Trim().ToLowerInvariant(),
            EncryptedContent = encrypted,
            SizeBytes = request.Content.LongLength,
        };

        await onboardingRepository.AddProtectedAssetAsync(asset, cancellationToken).ConfigureAwait(false);
        await onboardingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UploadedAssetDto(assetKey, category, asset.FileName, asset.ContentType, asset.SizeBytes);
    }

    internal static bool HasValidAssetSignature(string extension, byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return false;
        }

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 5
                && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;
        }

        bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        bool isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        bool isWebP = bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

        return isPng || isJpeg || isWebP;
    }
}

public sealed class GetProtectedAssetQueryHandler(
    IOnboardingRepository onboardingRepository,
    IPiiProtectionService piiProtectionService,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetProtectedAssetQuery, ProtectedAssetDownloadDto>
{
    public async Task<ProtectedAssetDownloadDto> Handle(GetProtectedAssetQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            throw new AuthenticationFailedException("Authentication is required.");
        }

        ProtectedAsset asset = await onboardingRepository
            .GetProtectedAssetAsync(request.AssetKey.Trim(), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ProtectedAsset), request.AssetKey);

        if (currentUserService.Role != ApplicationRole.SuperAdmin)
        {
            if (asset.TenantId.HasValue)
            {
                if (!tenantContext.CurrentTenantId.HasValue || tenantContext.CurrentTenantId.Value != asset.TenantId.Value)
                {
                    throw new TenantAccessDeniedException("Cross-tenant access to private asset denied.");
                }
            }
            else if (currentUserService.UserId != asset.OwnerUserId)
            {
                throw new TenantAccessDeniedException("Access to private document denied.");
            }
        }

        byte[] decrypted = piiProtectionService.DecryptBytes(asset.EncryptedContent);
        return new ProtectedAssetDownloadDto(asset.FileName, asset.ContentType, decrypted);
    }
}

