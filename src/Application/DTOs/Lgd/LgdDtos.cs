using System.Text.Json.Serialization;

namespace GPMH.Application.DTOs.Lgd;

public sealed record LgdDistrictDto(
    [property: JsonPropertyName("district")] string District,
    [property: JsonPropertyName("district_code")] string? DistrictCode,
    [property: JsonPropertyName("gp_count")] int GpCount);

public sealed record LgdTehsilDto(
    [property: JsonPropertyName("tehsil")] string Tehsil,
    [property: JsonPropertyName("tehsil_code")] string? TehsilCode,
    [property: JsonPropertyName("gp_count")] int GpCount);

public sealed record LgdPanchayatSummaryDto(
    [property: JsonPropertyName("lgd_code")] string LgdCode,
    [property: JsonPropertyName("gp_name_en")] string GpNameEn,
    [property: JsonPropertyName("gp_name_mr")] string GpNameMr,
    [property: JsonPropertyName("district")] string District,
    [property: JsonPropertyName("tehsil")] string Tehsil,
    [property: JsonPropertyName("pincode")] string? Pincode);

public sealed record LgdPanchayatDetailDto(
    [property: JsonPropertyName("lgd_code")] string LgdCode,
    [property: JsonPropertyName("gp_name_en")] string GpNameEn,
    [property: JsonPropertyName("gp_name_mr")] string GpNameMr,
    [property: JsonPropertyName("district")] string District,
    [property: JsonPropertyName("district_code")] string? DistrictCode,
    [property: JsonPropertyName("tehsil")] string Tehsil,
    [property: JsonPropertyName("tehsil_code")] string? TehsilCode,
    [property: JsonPropertyName("pincode")] string? Pincode);

public sealed record LgdStatsDto(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("state_code")] int StateCode,
    [property: JsonPropertyName("total_districts")] int TotalDistricts,
    [property: JsonPropertyName("total_tehsils")] int TotalTehsils,
    [property: JsonPropertyName("total_panchayats")] int TotalPanchayats,
    [property: JsonPropertyName("api_version")] string ApiVersion);

