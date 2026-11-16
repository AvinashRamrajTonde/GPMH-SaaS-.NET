using GPMH.Application.DTOs.Citizens;
using GPMH.Domain.Entities.Assessments;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Tenant-scoped Citizen Register repository enforcing DPDP masked Aadhaar projection and blind-index lookup.
/// </summary>
public interface ICitizenRepository
{
    Task<PagedCitizensDto> SearchAsync(
        Guid tenantId,
        string? search,
        string? wardNo,
        string? exemptionCategory,
        string? exactAadhaarBlindIndex,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Citizen?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, Citizen>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<bool> ExistsCitizenCodeAsync(string citizenCode, Guid? excludeCitizenId, CancellationToken cancellationToken);

    Task<bool> ExistsAadhaarBlindIndexAsync(string blindIndex, Guid? excludeCitizenId, CancellationToken cancellationToken);

    Task<string> GenerateNextCitizenCodeAsync(CancellationToken cancellationToken);

    Task<int> GetLinkedPropertiesCountAsync(Guid citizenId, CancellationToken cancellationToken);

    Task AddAsync(Citizen citizen, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
