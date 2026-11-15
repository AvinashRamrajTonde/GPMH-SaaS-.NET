using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class PropertyRepository(AppDbContext dbContext) : IPropertyRepository
{
    public async Task<IReadOnlyList<Property>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Properties
            .AsNoTracking()
            .OrderBy(p => p.WardNo)
            .ThenBy(p => p.PropertyNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsByWardAndNoAsync(string wardNo, string propertyNo, CancellationToken cancellationToken) =>
        dbContext.Properties.AnyAsync(p => p.WardNo == wardNo && p.PropertyNo == propertyNo, cancellationToken);

    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        await dbContext.Properties.AddAsync(property, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

