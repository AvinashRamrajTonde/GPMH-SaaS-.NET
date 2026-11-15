using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;

namespace GPMH.Infrastructure.Services.LgdDirectory;

/// <summary>
/// Typed HTTP client for the Maharashtra Gram Panchayat Directory REST API.
/// Enforces trailing '/' on BaseAddress so "/api/v1/" is preserved on relative routes.
/// </summary>
public sealed class LgdDirectoryClient(HttpClient httpClient) : ILgdDirectoryClient
{
    public static Uri NormalizeBaseUri(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new ArgumentException("LGD API base URL cannot be empty.", nameof(rawUrl));
        }

        string normalized = rawUrl.TrimEnd('/') + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    public async Task<IReadOnlyList<LgdDistrictDto>> GetDistrictsAsync(CancellationToken cancellationToken)
    {
        LgdEnvelope<LgdDistrictsPayload>? envelope = await httpClient
            .GetFromJsonAsync<LgdEnvelope<LgdDistrictsPayload>>("districts", cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data?.Districts ?? [];
    }

    public async Task<IReadOnlyList<LgdTehsilDto>> GetTehsilsAsync(string district, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        string uri = $"tehsils?district={Uri.EscapeDataString(district.Trim())}";

        LgdEnvelope<LgdTehsilsPayload>? envelope = await httpClient
            .GetFromJsonAsync<LgdEnvelope<LgdTehsilsPayload>>(uri, cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data?.Tehsils ?? [];
    }

    public async Task<IReadOnlyList<LgdPanchayatSummaryDto>> GetPanchayatsAsync(
        string district,
        string tehsil,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentException.ThrowIfNullOrWhiteSpace(tehsil);

        string uri = $"panchayats?district={Uri.EscapeDataString(district.Trim())}&tehsil={Uri.EscapeDataString(tehsil.Trim())}";
        LgdEnvelope<LgdPanchayatsPayload>? envelope = await httpClient
            .GetFromJsonAsync<LgdEnvelope<LgdPanchayatsPayload>>(uri, cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data?.Panchayats ?? [];
    }

    public async Task<LgdPanchayatDetailDto?> GetPanchayatByLgdCodeAsync(
        string lgdCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lgdCode);
        string uri = $"panchayat/{Uri.EscapeDataString(lgdCode.Trim())}";

        using HttpResponseMessage response = await httpClient
            .GetAsync(uri, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        LgdEnvelope<LgdPanchayatDetailDto>? envelope = await response.Content
            .ReadFromJsonAsync<LgdEnvelope<LgdPanchayatDetailDto>>(cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data;
    }

    public async Task<IReadOnlyList<LgdPanchayatSummaryDto>> SearchPanchayatsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        int clampedLimit = Math.Clamp(limit, 1, 100);
        string uri = $"search?q={Uri.EscapeDataString(query.Trim())}&limit={clampedLimit}";

        LgdEnvelope<LgdSearchPayload>? envelope = await httpClient
            .GetFromJsonAsync<LgdEnvelope<LgdSearchPayload>>(uri, cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data?.Results ?? [];
    }

    public async Task<LgdStatsDto?> GetStatsAsync(CancellationToken cancellationToken)
    {
        LgdEnvelope<LgdStatsDto>? envelope = await httpClient
            .GetFromJsonAsync<LgdEnvelope<LgdStatsDto>>("stats", cancellationToken)
            .ConfigureAwait(false);

        return envelope?.Data;
    }

    private sealed record LgdEnvelope<T>(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] T? Data);

    private sealed record LgdDistrictsPayload(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("districts")] IReadOnlyList<LgdDistrictDto> Districts);

    private sealed record LgdTehsilsPayload(
        [property: JsonPropertyName("district")] string District,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("tehsils")] IReadOnlyList<LgdTehsilDto> Tehsils);

    private sealed record LgdPanchayatsPayload(
        [property: JsonPropertyName("district")] string District,
        [property: JsonPropertyName("tehsil")] string Tehsil,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("panchayats")] IReadOnlyList<LgdPanchayatSummaryDto> Panchayats);

    private sealed record LgdSearchPayload(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("results")] IReadOnlyList<LgdPanchayatSummaryDto> Results);
}

