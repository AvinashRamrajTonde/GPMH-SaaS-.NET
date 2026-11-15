namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Multi-tenant distributed cache abstraction (Redis in production, memory cache in dev/tests).
/// Keys MUST be tenant-prefixed via BuildTenantKey to prevent cross-tenant cache bleed.
/// </summary>
public interface ICacheService
{
    static string BuildTenantKey(Guid tenantId, string keySuffix) =>
        $"tenant:{tenantId:D}:{keySuffix.Trim()}";

    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

