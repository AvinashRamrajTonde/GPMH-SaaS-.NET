using GPMH.Application.DTOs.Properties;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Tenancy;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Tenant-scoped persistence abstraction for Namuna 8 assessable properties, owners, floors, tax snapshots, and demands.
/// Implementations apply EF Core global tenant filters and PostgreSQL RLS automatically.
/// </summary>
public interface IPropertyRepository
{
    Task<IReadOnlyList<Property>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PropertyDto>> ListEnrichedAsync(CancellationToken cancellationToken);

    Task<PropertyHubKpisDto> GetHubKpisAsync(CancellationToken cancellationToken);

    Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PropertyDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByWardAndNoAsync(string wardNo, string propertyNo, CancellationToken cancellationToken);

    Task<AssessmentYear?> GetActiveAssessmentYearAsync(CancellationToken cancellationToken);

    Task AddAsync(Property property, CancellationToken cancellationToken);

    Task PersistFullAssessmentAsync(
        Property property,
        IReadOnlyList<PropertyOwner>? owners,
        IReadOnlyList<PropertyConstruction>? constructions,
        PropertyTaxCalc? taxCalc,
        PropertyDemand? draftDemand,
        bool replaceOwnersAndConstructions,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
