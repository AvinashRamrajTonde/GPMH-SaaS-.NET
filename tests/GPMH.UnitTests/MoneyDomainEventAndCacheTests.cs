using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Domain.Events;
using GPMH.Domain.ValueObjects;
using GPMH.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace GPMH.UnitTests;

public sealed class MoneyDomainEventAndCacheTests
{
    private sealed record CachedTaxSeedSummary(string Code, decimal Rate);

    [Fact]
    public void Money_EnforcesDecimalAwayFromZeroAndCurrencySafety()
    {
        Money a = new(100.125m);
        Money b = new(45.10m);
        Money sum = a + b;

        Assert.Equal(100.13m, a.Amount);
        Assert.Equal("INR", a.Currency);
        Assert.Equal(145.23m, sum.Amount);

        Money usd = new(10m, "usd");
        Assert.Throws<InvalidOperationException>(() => a + usd);
    }

    [Fact]
    public void Tenant_Activate_RaisesTenantOnboardedDomainEvent()
    {
        Tenant tenant = new()
        {
            TenantCode = "GP-171391",
            LgdCode = "171391",
            NameEn = "Chincholi Khurd",
            NameMr = "चिंचोली खुर्द",
        };

        DateTimeOffset now = DateTimeOffset.UtcNow;
        tenant.Activate(now);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        TenantOnboardedDomainEvent domainEvent = Assert.IsType<TenantOnboardedDomainEvent>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(tenant.Id, domainEvent.TenantId);
        Assert.Equal("171391", domainEvent.LgdCode);

        tenant.ClearDomainEvents();
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public async Task DistributedCacheService_ScopesKeysPerTenantAndRoundTripsJson()
    {
        MemoryDistributedCache memCache = new(Options.Create(new MemoryDistributedCacheOptions()));
        DistributedCacheService cache = new(memCache);

        Guid tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        string key = ICacheService.BuildTenantKey(tenantId, "formula-settings");

        Assert.Equal("tenant:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee:formula-settings", key);

        CachedTaxSeedSummary expected = new("HUT_MUD", 0.5m);
        await cache.SetAsync(key, expected, TimeSpan.FromMinutes(5));

        CachedTaxSeedSummary? actual = await cache.GetAsync<CachedTaxSeedSummary>(key);
        Assert.Equal(expected, actual);

        await cache.RemoveAsync(key);
        CachedTaxSeedSummary? removed = await cache.GetAsync<CachedTaxSeedSummary>(key);
        Assert.Null(removed);
    }
}

