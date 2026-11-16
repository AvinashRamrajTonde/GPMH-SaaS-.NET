using GPMH.Application.DTOs.Mutations;
using GPMH.Domain.Entities.Assessments;

namespace GPMH.Application.Common.Interfaces;

public interface IMutationRepository
{
    Task<PagedMutationsDto> SearchAsync(
        Guid tenantId,
        string? status,
        string? search,
        string? wardNo,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MutationDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PropertyMutation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<string> GenerateNextApplicationNoAsync(int startYear, CancellationToken cancellationToken);

    Task<IReadOnlyList<MutationOwnerSnapshotItem>> LoadCurrentPropertyOwnersAsync(
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MutationSupportingAssetDto>> ValidateTenantAssetKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<string> assetKeys,
        CancellationToken cancellationToken);

    Task AddAsync(PropertyMutation mutation, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ApproveMutationAtomicallyAsync(
        PropertyMutation mutation,
        IReadOnlyList<PropertyOwner> replacementOwners,
        string auditDetailsJson,
        CancellationToken cancellationToken);

    Task<MutationRegisterPdfDocumentModel> LoadRegisterPdfModelAsync(
        Guid tenantId,
        string? wardNo,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken);
}

