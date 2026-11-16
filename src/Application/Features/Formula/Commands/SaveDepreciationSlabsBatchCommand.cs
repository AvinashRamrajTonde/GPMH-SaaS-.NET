using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record DepreciationSlabBatchItem(
    int AgeFrom,
    int AgeTo,
    decimal HutMudPercent,
    decimal StoneBrickRccPercent);

public sealed record SaveDepreciationSlabsBatchCommand(
    IReadOnlyList<DepreciationSlabBatchItem> Slabs,
    string ResolutionNo,
    DateOnly? ResolutionDate,
    string ChangeReason) : IRequest<IReadOnlyList<DepreciationSlabDto>>;

public sealed class SaveDepreciationSlabsBatchCommandValidator
    : AbstractValidator<SaveDepreciationSlabsBatchCommand>
{
    public SaveDepreciationSlabsBatchCommandValidator()
    {
        RuleFor(x => x.Slabs)
            .NotEmpty()
            .WithMessage("At least one depreciation slab must be supplied.");

        RuleForEach(x => x.Slabs).ChildRules(slab =>
        {
            slab.RuleFor(s => s.AgeFrom).GreaterThanOrEqualTo(0);
            slab.RuleFor(s => s.AgeTo).GreaterThan(s => s.AgeFrom)
                .WithMessage("AgeTo must be strictly greater than AgeFrom.");
            slab.RuleFor(s => s.HutMudPercent).InclusiveBetween(0m, 100m);
            slab.RuleFor(s => s.StoneBrickRccPercent).InclusiveBetween(0m, 100m);
        });

        RuleFor(x => x.Slabs).Custom((slabs, ctx) =>
        {
            if (slabs is null || slabs.Count == 0)
            {
                return;
            }

            List<DepreciationSlabBatchItem> ordered = [.. slabs.OrderBy(s => s.AgeFrom).ThenBy(s => s.AgeTo)];
            if (ordered[0].AgeFrom != 0)
            {
                ctx.AddFailure(nameof(SaveDepreciationSlabsBatchCommand.Slabs), "First depreciation slab must start at AgeFrom = 0.");
                return;
            }

            for (int i = 1; i < ordered.Count; i++)
            {
                DepreciationSlabBatchItem prev = ordered[i - 1];
                DepreciationSlabBatchItem curr = ordered[i];
                if (curr.AgeFrom != prev.AgeTo)
                {
                    ctx.AddFailure(
                        nameof(SaveDepreciationSlabsBatchCommand.Slabs),
                        $"Depreciation slabs must be contiguous with no gaps or overlaps: slab starting at {curr.AgeFrom} must equal previous AgeTo {prev.AgeTo}.");
                    return;
                }
            }

            if (ordered[^1].AgeTo < 100)
            {
                ctx.AddFailure(
                    nameof(SaveDepreciationSlabsBatchCommand.Slabs),
                    "Final depreciation slab must cover at least up to age 100.");
            }
        });

        RuleFor(x => x.ResolutionNo)
            .NotEmpty()
            .WithMessage("GP Resolution No is required for depreciation schedule changes.")
            .MaximumLength(80);
        RuleFor(x => x.ChangeReason)
            .NotEmpty()
            .WithMessage("Change reason is required for audit traceability.")
            .MaximumLength(400);
    }
}

public sealed class SaveDepreciationSlabsBatchCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<SaveDepreciationSlabsBatchCommand, IReadOnlyList<DepreciationSlabDto>>
{
    public async Task<IReadOnlyList<DepreciationSlabDto>> Handle(
        SaveDepreciationSlabsBatchCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string resolutionNo = request.ResolutionNo.Trim();
        string changeReason = request.ChangeReason.Trim();

        List<DepreciationSlab> orderedEntities = [.. request.Slabs
            .OrderBy(s => s.AgeFrom)
            .ThenBy(s => s.AgeTo)
            .Select(s => new DepreciationSlab
            {
                TenantId = tenantId,
                AgeFrom = s.AgeFrom,
                AgeTo = s.AgeTo,
                FromInclusive = s.AgeFrom == 0,
                ToInclusive = true,
                HutMudPercent = s.HutMudPercent,
                StoneBrickRccPercent = s.StoneBrickRccPercent,
                LastResolutionNo = resolutionNo,
                LastResolutionDate = request.ResolutionDate,
                LastChangeReason = changeReason,
            })];

        await formulaRepository
            .ReplaceDepreciationSlabsAsync(tenantId, orderedEntities, cancellationToken)
            .ConfigureAwait(false);
        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DepreciationSlab> saved = await formulaRepository
            .ListDepreciationSlabsAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. saved.Select(s => s.ToDto())];
    }
}

