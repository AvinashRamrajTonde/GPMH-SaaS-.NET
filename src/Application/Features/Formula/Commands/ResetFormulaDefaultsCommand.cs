using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Application.Features.Formula.Queries;
using MediatR;

namespace GPMH.Application.Features.Formula.Commands;

public sealed record ResetFormulaDefaultsCommand(
    string? ResolutionNo = null,
    DateOnly? ResolutionDate = null,
    string? ResolutionReason = null) : IRequest<FormulaWorkspaceDto>;

public sealed class ResetFormulaDefaultsCommandValidator : AbstractValidator<ResetFormulaDefaultsCommand>
{
    public ResetFormulaDefaultsCommandValidator()
    {
        RuleFor(x => x.ResolutionNo).MaximumLength(80);
        RuleFor(x => x.ResolutionReason).MaximumLength(400);
    }
}

public sealed class ResetFormulaDefaultsCommandHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService,
    ISender sender)
    : IRequestHandler<ResetFormulaDefaultsCommand, FormulaWorkspaceDto>
{
    public async Task<FormulaWorkspaceDto> Handle(
        ResetFormulaDefaultsCommand request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string resolutionNo = string.IsNullOrWhiteSpace(request.ResolutionNo)
            ? "STATUTORY-ADR016"
            : request.ResolutionNo.Trim();
        string reason = string.IsNullOrWhiteSpace(request.ResolutionReason)
            ? "Restored canonical ADR-016 Maharashtra statutory formula defaults."
            : request.ResolutionReason.Trim();

        await formulaRepository
            .ResetCanonicalDefaultsAsync(tenantId, resolutionNo, request.ResolutionDate, reason, cancellationToken)
            .ConfigureAwait(false);

        await formulaRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(FormulaCacheKeys.Workspace(tenantId), cancellationToken).ConfigureAwait(false);

        return await sender.Send(new GetFormulaSettingsWorkspaceQuery(), cancellationToken).ConfigureAwait(false);
    }
}

