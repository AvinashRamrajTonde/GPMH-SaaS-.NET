using GPMH.Application.DTOs.Formula;
using GPMH.Domain.Entities.Taxation;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Tenant-scoped repository for statutory taxation formula settings and valuation masters.
/// Implementations rely on AppDbContext global tenant filters + explicit tenant checks.
/// </summary>
public interface IFormulaMasterRepository
{
    Task<IReadOnlyList<FormulaSetting>> ListFormulaSettingsAsync(CancellationToken cancellationToken);

    Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken);

    Task<FormulaSetting?> GetFormulaSettingByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<FormulaSetting?> GetFormulaSettingByPeriodNameAsync(string ratePeriodName, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConstructionTypeMaster>> ListConstructionTypesAsync(CancellationToken cancellationToken);

    Task<ConstructionTypeMaster?> GetConstructionTypeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ConstructionTypeMaster?> GetConstructionTypeByCodeAsync(string code, CancellationToken cancellationToken);

    Task<FormulaWorkspaceSummaryDto> GetWorkspaceSummaryCountsAsync(CancellationToken cancellationToken);

    Task AddFormulaSettingAsync(FormulaSetting entity, CancellationToken cancellationToken);

    Task AddConstructionTypeAsync(ConstructionTypeMaster entity, CancellationToken cancellationToken);

    Task ResetCanonicalDefaultsAsync(
        Guid tenantId,
        string? resolutionNo,
        DateOnly? resolutionDate,
        string? changeReason,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

