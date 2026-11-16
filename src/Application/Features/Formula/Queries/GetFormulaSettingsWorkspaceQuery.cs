using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Formula;
using GPMH.Domain.Entities.Taxation;
using MediatR;

namespace GPMH.Application.Features.Formula.Queries;

internal static class FormulaCacheKeys
{
    public const string WorkspaceSuffix = "formula:workspace";

    public static string Workspace(Guid tenantId) =>
        ICacheService.BuildTenantKey(tenantId, WorkspaceSuffix);
}

public sealed record GetFormulaSettingsWorkspaceQuery : IRequest<FormulaWorkspaceDto>;

public sealed class GetFormulaSettingsWorkspaceQueryHandler(
    IFormulaMasterRepository formulaRepository,
    ITenantContext tenantContext,
    ICacheService cacheService)
    : IRequestHandler<GetFormulaSettingsWorkspaceQuery, FormulaWorkspaceDto>
{
    private static readonly TimeSpan WorkspaceCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<FormulaWorkspaceDto> Handle(
        GetFormulaSettingsWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.CurrentTenantId
            ?? throw new TenantAccessDeniedException("Authenticated tenant context is required.");

        string cacheKey = FormulaCacheKeys.Workspace(tenantId);
        FormulaWorkspaceDto? cached = await cacheService
            .GetAsync<FormulaWorkspaceDto>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cached is not null)
        {
            return cached;
        }

        IReadOnlyList<FormulaSetting> settings = await formulaRepository
            .ListFormulaSettingsAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ConstructionTypeMaster> constructionTypes = await formulaRepository
            .ListConstructionTypesAsync(cancellationToken)
            .ConfigureAwait(false);
        FormulaWorkspaceSummaryDto summary = await formulaRepository
            .GetWorkspaceSummaryCountsAsync(cancellationToken)
            .ConfigureAwait(false);

        List<FormulaSettingDto> periodDtos = [.. settings.Select(s => s.ToDto())];
        FormulaSettingDto? activeSetting = periodDtos.FirstOrDefault(s => s.IsActive)
            ?? periodDtos.FirstOrDefault();

        FormulaWorkspaceDto workspace = new(
            ActiveSetting: activeSetting,
            RatePeriods: periodDtos,
            ConstructionTypes: [.. constructionTypes.Select(c => c.ToDto())],
            Summary: summary);

        await cacheService
            .SetAsync(cacheKey, workspace, WorkspaceCacheTtl, cancellationToken)
            .ConfigureAwait(false);

        return workspace;
    }
}

