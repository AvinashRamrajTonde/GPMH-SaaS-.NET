using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record AreaSlabTaxBatchItem(
    int AreaFrom,
    int AreaTo,
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax);

public sealed record SaveAreaSlabTaxesBatchCommand(
    IReadOnlyList<AreaSlabTaxBatchItem> Slabs,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason) : IRequest<IReadOnlyList<AreaSlabTaxDto>>;

public sealed class SaveAreaSlabTaxesBatchCommandValidator
    : AbstractValidator<SaveAreaSlabTaxesBatchCommand>
{
    public SaveAreaSlabTaxesBatchCommandValidator()
    {
        RuleFor(x => x.Slabs)
            .NotEmpty()
            .WithMessage("At least one area slab tax range must be supplied.");

        RuleForEach(x => x.Slabs).ChildRules(slab =>
        {
            slab.RuleFor(s => s.AreaFrom).GreaterThanOrEqualTo(1);
            slab.RuleFor(s => s.AreaTo).GreaterThanOrEqualTo(s => s.AreaFrom)
                .WithMessage("AreaTo must be greater than or equal to AreaFrom.");
            slab.RuleFor(s => s.LightingTax).GreaterThanOrEqualTo(0m);
            slab.RuleFor(s => s.HealthTax).GreaterThanOrEqualTo(0m);
            slab.RuleFor(s => s.SanitationTax).GreaterThanOrEqualTo(0m);
            slab.RuleFor(s => s.ToiletTax).GreaterThanOrEqualTo(0m);
        });

        RuleFor(x => x.Slabs).Custom((slabs, ctx) =>
        {
            if (slabs is null || slabs.Count == 0)
            {
                return;
            }

            List<AreaSlabTaxBatchItem> ordered = [.. slabs.OrderBy(s => s.AreaFrom).ThenBy(s => s.AreaTo)];
            if (ordered[0].AreaFrom != 1)
            {
                ctx.AddFailure(nameof(SaveAreaSlabTaxesBatchCommand.Slabs), "First area slab must start at AreaFrom = 1 sq.ft.");
                return;
            }

            for (int i = 1; i < ordered.Count; i++)
            {
                AreaSlabTaxBatchItem prev = ordered[i - 1];
                AreaSlabTaxBatchItem curr = ordered[i];
                if (curr.AreaFrom != prev.AreaTo + 1)
                {
                    ctx.AddFailure(
                        nameof(SaveAreaSlabTaxesBatchCommand.Slabs),
                        $"Area slabs must be contiguous and non-overlapping: slab starting at {curr.AreaFrom} must equal previous AreaTo + 1 ({prev.AreaTo + 1}).");
                    return;
                }
            }
        });

        RuleFor(x => x.ResolutionNo)
            .NotEmpty()
            .WithMessage("GP Resolution No is required for area slab cess changes.")
            .MaximumLength(80);
        RuleFor(x => x.ChangeReason)
            .NotEmpty()
            .WithMessage("Change reason is required for audit traceability.")
            .MaximumLength(400);
    }
}

public sealed class SaveAreaSlabTaxesBatchCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveAreaSlabTaxesBatchCommand, IReadOnlyList<AreaSlabTaxDto>>
{
    public async Task<IReadOnlyList<AreaSlabTaxDto>> Handle(
        SaveAreaSlabTaxesBatchCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string resolutionNo = request.ResolutionNo.Trim();
        string changeReason = request.ChangeReason.Trim();

        List<AreaSlabTax> orderedEntities = [.. request.Slabs
            .OrderBy(s => s.AreaFrom)
            .ThenBy(s => s.AreaTo)
            .Select(s => new AreaSlabTax
            {
                TenantId = tenantId,
                AreaFrom = s.AreaFrom,
                AreaTo = s.AreaTo,
                LightingTax = s.LightingTax,
                HealthTax = s.HealthTax,
                SanitationTax = s.SanitationTax,
                ToiletTax = s.ToiletTax,
                LastResolutionNo = resolutionNo,
                LastResolutionDate = request.ResolutionDate,
                LastChangeReason = changeReason,
            })];

        await formulaRepository
            .ReplaceAreaSlabTaxesAsync(tenantId, orderedEntities, cancellationToken)
            .ConfigureAwait(false);
        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        IReadOnlyList<AreaSlabTax> saved = await formulaRepository
            .ListAreaSlabTaxesAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. saved.Select(s => s.ToDto())];
    }
}

