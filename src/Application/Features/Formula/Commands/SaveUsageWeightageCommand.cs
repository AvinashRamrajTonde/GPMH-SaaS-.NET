using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record SaveUsageWeightageCommand(
    Guid? Id,
    string Code,
    string UsageName,
    decimal Weightage,
    bool IsActive,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason) : IRequest<UsageWeightageDto>;

public sealed class SaveUsageWeightageCommandValidator : AbstractValidator<SaveUsageWeightageCommand>
{
    public SaveUsageWeightageCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(32)
            .Matches("^[A-Za-z0-9_\\-]+$")
            .WithMessage("Usage code may contain only letters, digits, hyphens, and underscores.");
        RuleFor(x => x.UsageName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Weightage).GreaterThan(0m).LessThanOrEqualTo(100m);
        RuleFor(x => x.ResolutionNo)
            .NotEmpty()
            .WithMessage("GP Resolution No is required for usage weightage updates.")
            .MaximumLength(80);
        RuleFor(x => x.ChangeReason)
            .NotEmpty()
            .WithMessage("Change reason is required for audit traceability.")
            .MaximumLength(400);
    }
}

public sealed class SaveUsageWeightageCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveUsageWeightageCommand, UsageWeightageDto>
{
    public async Task<UsageWeightageDto> Handle(
        SaveUsageWeightageCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string normalizedCode = request.Code.Trim().ToUpperInvariant();
        string usageName = request.UsageName.Trim();
        string resolutionNo = request.ResolutionNo.Trim();
        string changeReason = request.ChangeReason.Trim();

        UsageWeightage? entity;
        bool isNew = false;

        if (request.Id.HasValue)
        {
            entity = await formulaRepository
                .GetUsageWeightageByIdAsync(request.Id.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Usage weightage '{request.Id.Value}' was not found.");

            UsageWeightage? conflict = await formulaRepository
                .GetUsageWeightageByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);
            if (conflict is not null && conflict.Id != entity.Id)
            {
                throw new DomainValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(request.Code)] = [$"Usage code '{normalizedCode}' already exists in this Gram Panchayat."],
                });
            }
        }
        else
        {
            entity = await formulaRepository
                .GetUsageWeightageByCodeAsync(normalizedCode, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                entity = new UsageWeightage
                {
                    TenantId = tenantId,
                    Code = normalizedCode,
                    UsageName = usageName,
                };
                isNew = true;
            }
        }

        entity.Code = normalizedCode;
        entity.UsageName = usageName;
        entity.Weightage = request.Weightage;
        entity.IsActive = request.IsActive;
        entity.LastResolutionNo = resolutionNo;
        entity.LastResolutionDate = request.ResolutionDate;
        entity.LastChangeReason = changeReason;

        if (isNew)
        {
            await formulaRepository.AddUsageWeightageAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        return entity.ToDto();
    }
}

