using GPMH.Domain.Entities.Assessments;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Tenant-scoped persistence abstraction for Namuna 8 assessable properties.
/// Implementations apply EF Core global tenant filters and PostgreSQL RLS automatically.
/// </summary>
public interface IPropertyRepository
{
    Task<IReadOnlyList<Property>> ListAsync(CancellationToken cancellationToken);

    Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByWardAndNoAsync(string wardNo, string propertyNo, CancellationToken cancellationToken);

    Task AddAsync(Property property, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

