using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;

namespace GPMH.Application.Common.Interfaces;

public sealed record Namuna9DemandContext(
    PropertyDemand Demand,
    Property Property,
    string? OwnerNameMr,
    string? OwnerNameEn,
    PropertyTaxCalc? TaxCalc);

public sealed record Namuna9RolloverInputs(
    IReadOnlyList<Property> Properties,
    IReadOnlyDictionary<Guid, PropertyDemand> SourceDemands,
    IReadOnlyDictionary<Guid, PropertyTaxCalc> SourceCalcs,
    IReadOnlyDictionary<Guid, PropertyDemand> TargetDemands,
    IReadOnlyDictionary<Guid, PropertyTaxCalc> TargetCalcs);

/// <summary>Tenant-scoped persistence abstraction for the Namuna 9 demand register (EF tenant filters active).</summary>
public interface INamuna9Repository
{
    Task<IReadOnlyList<AssessmentYear>> ListYearsAsync(CancellationToken cancellationToken);

    /// <summary>Returns the tracked year by id, or the active year when <paramref name="yearId"/> is null.</summary>
    Task<AssessmentYear?> GetYearAsync(Guid? yearId, CancellationToken cancellationToken);

    Task<AssessmentYear?> FindYearByLabelAsync(string yearLabel, CancellationToken cancellationToken);

    Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Namuna9DemandContext>> LoadContextsAsync(
        Guid assessmentYearId,
        string? wardNo,
        string? search,
        IReadOnlyCollection<Guid>? propertyIds,
        CancellationToken cancellationToken);

    Task<Namuna9RolloverInputs> LoadRolloverInputsAsync(
        Guid sourceYearId,
        Guid? targetYearId,
        CancellationToken cancellationToken);

    Task AddYearAsync(AssessmentYear year, CancellationToken cancellationToken);

    Task AddDemandsAsync(IEnumerable<PropertyDemand> demands, CancellationToken cancellationToken);

    Task AddTaxCalcsAsync(IEnumerable<PropertyTaxCalc> calcs, CancellationToken cancellationToken);

    Task AddAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Namuna9GramPanchayatHeaderDto> LoadPrintHeaderAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="work"/> (including its SaveChanges) inside one ACID transaction.</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}

