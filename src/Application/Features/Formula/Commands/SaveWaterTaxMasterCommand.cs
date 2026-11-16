using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record SaveWaterTaxMasterCommand(
    Guid? Id,
    string Code,
    string WaterTypeName,
    decimal Amount,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason) : IRequest<WaterTaxMasterDto>;

public sealed class SaveWaterTaxMasterCommandValidator : AbstractValidator<SaveWaterTaxMasterCommand>
{
    public SaveWaterTaxMasterCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(32)
            .Matches("^[A-Za-z0-9_\\-]+$")
            .WithMessage("Water tax code may contain only letters, digits, hyphens, and underscores.");
        RuleFor(x => x.WaterTypeName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.ResolutionNo)
            .NotEmpty()
            .WithMessage("GP Resolution No is required for water tax updates.")
            .MaximumLength(80);
        RuleFor(x => x.ChangeReason)
            .NotEmpty()
            .WithMessage("Change reason is required for audit traceability.")
            .MaximumLength(400);
    }
}

public sealed class SaveWaterTaxMasterCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveWaterTaxMasterCommand, WaterTaxMasterDto>
{
    public async Task<WaterTaxMasterDto> Handle(
        SaveWaterTaxMasterCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string normalizedCode = request.Code.Trim().ToUpperInvariant();
        string waterTypeName = request.WaterTypeName.Trim();
        string resolutionNo = request.ResolutionNo.Trim();
        string changeReason = request.ChangeReason.Trim();

        WaterTaxMaster? entity;
        bool isNew = false;

        if (request.Id.HasValue)
        {
            entity = await formulaRepository
                .GetWaterTaxByIdAsync(request.Id.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Water tax master '{request.Id.Value}' was not found.");

            WaterTaxMaster? conflict = await formulaRepository
                .GetWaterTaxByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);
            if (conflict is not null && conflict.Id != entity.Id)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.Code)] = [$"Water tax code '{normalizedCode}' already exists in this Gram Panchayat."],
                });
            }
        }
        else
        {
            entity = await formulaRepository
                .GetWaterTaxByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                entity = new WaterTaxMaster
                {
                    TenantId = tenantId,
                    Code = normalizedCode,
                    WaterTypeName = waterTypeName,
                };
                isNew = true;
            }
        }

        entity.Code = normalizedCode;
        entity.WaterTypeName = waterTypeName;
        entity.Amount = request.Amount;
        entity.IsActive = request.IsActive;
        entity.LastResolutionNo = resolutionNo;
        entity.LastResolutionDate = request.ResolutionDate;
        entity.LastChangeReason = changeReason;

        if (isNew)
        {
            await formulaRepository.AddWaterTaxMasterAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        return entity.ToDto();
    }
}

