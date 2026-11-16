using GPMH.Application.DTOs.Partitions;
using GPMH.Domain.Entities.Assessments;

namespace GPMH.Application.Common.Interfaces;

public sealed record PartitionContextData(
    Property Property,
    IReadOnlyList<PartitionOwnerItemDto> CurrentOwners,
    IReadOnlyList<PropertyConstruction> CurrentFloors,
    IReadOnlyList<PropertyDemand> Demands);

/// <summary>
/// Tenant-scoped persistence abstraction for Namuna 8 property sub-division eligibility checks and ACID partition execution.
/// </summary>
public interface IPartitionRepository
{
    Task<PartitionContextData?> LoadPartitionContextAsync(Guid propertyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> FindConflictingPropertyNosAsync(
        string wardNo,
        IReadOnlyCollection<string> candidatePropertyNos,
        Guid excludePropertyId,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetExistingProtectedAssetKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<string> assetKeys,
        CancellationToken cancellationToken);

    Task ExecutePartitionAtomicallyAsync(
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
        CancellationToken cancellationToken);
}

