namespace GPMH.Application.DTOs.Citizens;

public sealed record CitizenDto(
    Guid Id,
    Guid TenantId,
    string CitizenCode,
    string? WardNo,
    string FullNameMr,
    string FullNameEn,
    string Gender,
    string? MobileNo,
    string? AadhaarMasked,
    string SocialCategory,
    string ExemptionCategory,
    bool IsActive,
    int LinkedPropertiesCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PagedCitizensDto(
    IReadOnlyList<CitizenDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

