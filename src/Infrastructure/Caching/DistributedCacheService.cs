using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace GPMH.Infrastructure.Caching;

public sealed class DistributedCacheService(IDistributedCache distributedCache) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        byte[]? bytes = await distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? DefaultTtl,
        };

        await distributedCache.SetAsync(key, payload, options, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return distributedCache.RemoveAsync(key, cancellationToken);
    }
}

