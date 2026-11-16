using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Partitions;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>
/// Tenant-scoped EF Core repository for Namuna 8 property sub-division eligibility checks and ACID partitions.
/// </summary>
public sealed class PartitionRepository(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : IPartitionRepository
{
    public async Task<PartitionContextData?> LoadPartitionContextAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        Property? property = await dbContext.Properties
            .FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return null;
        }

        var ownerRows = await (
            from po in dbContext.PropertyOwners
            join c in dbContext.Citizens on po.CitizenId equals c.Id
            where po.PropertyId == propertyId && po.IsActive
            select new
            {
                po.CitizenId,
                c.FullNameMr,
                c.FullNameEn,
                po.OwnershipSharePct,
                po.IsPrimaryOwner,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PartitionOwnerItemDto> currentOwners =
        [
            .. ownerRows
                .OrderByDescending(o => o.IsPrimaryOwner)
                .ThenBy(o => o.FullNameEn, StringComparer.OrdinalIgnoreCase)
                .Select(o => new PartitionOwnerItemDto(
                    o.CitizenId,
                    o.FullNameMr,
                    o.FullNameEn,
                    o.OwnershipSharePct,
                    o.IsPrimaryOwner)),
        ];

        List<PropertyConstruction> floorRows = await dbContext.PropertyConstructions
            .AsNoTracking()
            .Where(fc => fc.PropertyId == propertyId && fc.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PropertyConstruction> currentFloors =
        [
            .. floorRows.OrderBy(f => f.FloorNo),
        ];

        // Exclude locked/rolled-over historical years so carried-forward arrears are not double-counted against Zero-Dues Gate.
        List<PropertyDemand> demands = await (
            from d in dbContext.PropertyDemands.AsNoTracking()
            join y in dbContext.AssessmentYears.AsNoTracking() on d.AssessmentYearId equals y.Id
            where d.PropertyId == propertyId && !y.IsLocked
            select d)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PartitionContextData(
            Property: property,
            CurrentOwners: currentOwners,
            CurrentFloors: currentFloors,
            Demands: demands);
    }

    public async Task<IReadOnlyList<string>> FindConflictingPropertyNosAsync(
        string wardNo,
        IReadOnlyCollection<string> candidatePropertyNos,
        Guid excludePropertyId,
        CancellationToken cancellationToken)
    {
        string normalizedWard = wardNo.Trim();
        List<string> existingWardPropertyNos = await dbContext.Properties
            .AsNoTracking()
            .Where(p => p.WardNo == normalizedWard && p.Id != excludePropertyId)
            .Select(p => p.PropertyNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> candidateSet = new(
            candidatePropertyNos.Select(c => c.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return
        [
            .. existingWardPropertyNos
                .Where(existing => candidateSet.Contains(existing.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<IReadOnlySet<string>> GetExistingProtectedAssetKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<string> assetKeys,
        CancellationToken cancellationToken)
    {
        List<string> found = await dbContext.ProtectedAssets
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && assetKeys.Contains(a.AssetKey))
            .Select(a => a.AssetKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(found, StringComparer.Ordinal);
    }

    public async Task ExecutePartitionAtomicallyAsync(
        Property parentProperty,
        string archivedParentPropertyNo,
        IReadOnlyList<Property> childProperties,
        IReadOnlyList<PropertyOwner> childOwners,
        IReadOnlyList<PropertyConstruction> childConstructions,
        IReadOnlyList<PropertyTaxCalc> childTaxCalcs,
        IReadOnlyList<PropertyDemand> childDemands,
        PropertyMutation partitionMutation,
        string auditOldJson,
        string auditNewJson,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            parentProperty.PropertyNo = archivedParentPropertyNo;
            parentProperty.Status = "Partitioned";
            parentProperty.IsActive = false;

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (childProperties.Count > 0)
            {
                await dbContext.Properties.AddRangeAsync(childProperties, cancellationToken).ConfigureAwait(false);
            }

            if (childOwners.Count > 0)
            {
                await dbContext.PropertyOwners.AddRangeAsync(childOwners, cancellationToken).ConfigureAwait(false);
            }

            if (childConstructions.Count > 0)
            {
                await dbContext.PropertyConstructions.AddRangeAsync(childConstructions, cancellationToken).ConfigureAwait(false);
            }

            if (childTaxCalcs.Count > 0)
            {
                await dbContext.PropertyTaxCalcs.AddRangeAsync(childTaxCalcs, cancellationToken).ConfigureAwait(false);
            }

            if (childDemands.Count > 0)
            {
                await dbContext.PropertyDemands.AddRangeAsync(childDemands, cancellationToken).ConfigureAwait(false);
            }

            await dbContext.PropertyMutations.AddAsync(partitionMutation, cancellationToken).ConfigureAwait(false);

            await dbContext.AuditLogs.AddAsync(new AuditLog
            {
                TenantId = parentProperty.TenantId,
                EntityName = nameof(Property),
                EntityId = parentProperty.Id.ToString(),
                Action = "PropertyPartitioned",
                ActorId = currentUserService.UserId?.ToString(),
                ActorRole = currentUserService.Role?.ToString(),
                CorrelationId = currentUserService.CorrelationId,
                IpAddress = currentUserService.IpAddress,
                UserAgent = currentUserService.UserAgent,
                OldValuesJson = auditOldJson,
                NewValuesJson = auditNewJson,
                TimestampUtc = dateTimeProvider.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

