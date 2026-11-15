using GPMH.Application.DTOs.Lgd;

namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Read-only client to the public Maharashtra Gram Panchayat Directory REST API (external-api-proj).
/// </summary>
public interface ILgdDirectoryClient
{
    Task<IReadOnlyList<LgdDistrictDto>> GetDistrictsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LgdTehsilDto>> GetTehsilsAsync(string district, CancellationToken cancellationToken);
    Task<IReadOnlyList<LgdPanchayatSummaryDto>> GetPanchayatsAsync(string district, string tehsil, CancellationToken cancellationToken);
    Task<LgdPanchayatDetailDto?> GetPanchayatByLgdCodeAsync(string lgdCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<LgdPanchayatSummaryDto>> SearchPanchayatsAsync(string query, int limit, CancellationToken cancellationToken);
    Task<LgdStatsDto?> GetStatsAsync(CancellationToken cancellationToken);
}

