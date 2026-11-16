using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record SaveConstructionTypeCommand(
    Guid? Id,
    string Code,
    string TypeName,
    string Category,
    decimal LandAnnualValueRate,
    decimal ConstructionReadyReckonerRate,
    decimal UsageWeightageFactor,
    decimal TaxRate,
    bool IsActive = true,
    string? ResolutionNo = null,
    DateOnly? ResolutionDate = null,
    string? ChangeReason = null) : IRequest<ConstructionTypeMasterDto>;

public sealed class SaveConstructionTypeCommandValidator : AbstractValidator<SaveConstructionTypeCommand>
{
    public static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "HUT_MUD",
        "STONE_BRICK_RCC",
        "OPEN_LAND",
        "SPECIAL",
    };

    public SaveConstructionTypeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(40)
            .Matches("^[A-Za-z0-9_\\-]+$")
            .WithMessage("Construction type code may contain only letters, digits, hyphens, and underscores.");
        RuleFor(x => x.TypeName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(cat => cat is not null && AllowedCategories.Contains(cat.Trim()))
            .WithMessage("Category must be one of HUT_MUD, STONE_BRICK_RCC, OPEN_LAND, or SPECIAL.");
        RuleFor(x => x.LandAnnualValueRate).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.ConstructionReadyReckonerRate).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.UsageWeightageFactor).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.TaxRate).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.ResolutionNo).MaximumLength(80);
        RuleFor(x => x.ChangeReason).MaximumLength(400);
    }
}

public sealed class SaveConstructionTypeCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveConstructionTypeCommand, ConstructionTypeMasterDto>
{
    public async Task<ConstructionTypeMasterDto> Handle(
        SaveConstructionTypeCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string normalizedCode = request.Code.Trim().ToUpperInvariant();
        string normalizedCategory = request.Category.Trim().ToUpperInvariant();
        string normalizedTypeName = request.TypeName.Trim();
        string? resolutionNo = string.IsNullOrWhiteSpace(request.ResolutionNo) ? null : request.ResolutionNo.Trim();
        string? changeReason = string.IsNullOrWhiteSpace(request.ChangeReason) ? null : request.ChangeReason.Trim();

        ConstructionTypeMaster? entity;
        bool isNew = false;

        if (request.Id.HasValue)
        {
            entity = await formulaRepository
                .GetConstructionTypeByIdAsync(request.Id.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Construction type '{request.Id.Value}' was not found.");

            ConstructionTypeMaster? codeConflict = await formulaRepository
                .GetConstructionTypeByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);
            if (codeConflict is not null && codeConflict.Id != entity.Id)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.Code)] = [$"Construction type code '{normalizedCode}' already exists in this Gram Panchayat."],
                });
            }
        }
        else
        {
            // Upsert cleanly by tenant-unique Code so both POST and /api/Setting/SaveConstructionTypeAsync stay idempotent.
            entity = await formulaRepository
                .GetConstructionTypeByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                entity = new ConstructionTypeMaster
                {
                    TenantId = tenantId,
                    Code = normalizedCode,
                    TypeName = normalizedTypeName,
                    Category = normalizedCategory,
                };
                isNew = true;
            }
        }

        entity.Code = normalizedCode;
        entity.TypeName = normalizedTypeName;
        entity.Category = normalizedCategory;
        entity.LandAnnualValueRate = request.LandAnnualValueRate;
        entity.ConstructionReadyReckonerRate = request.ConstructionReadyReckonerRate;
        entity.UsageWeightageFactor = request.UsageWeightageFactor;
        entity.TaxRate = request.TaxRate;
        entity.IsActive = request.IsActive;
        entity.LastResolutionNo = resolutionNo;
        entity.LastResolutionDate = request.ResolutionDate;
        entity.LastChangeReason = changeReason;

        if (isNew)
        {
            await formulaRepository.AddConstructionTypeAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        return entity.ToDto();
    }
}

