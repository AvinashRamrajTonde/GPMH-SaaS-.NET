namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// Seeds canonical ADR-016 Maharashtra statutory tax rules (12 construction types, 9 depreciation slabs,
/// 3 usage weightages, 3 area slab taxes, 3 water tax heads, TaxDivisor = 1000) idempotently per tenant.
/// </summary>
public interface IDefaultFormulaSeeder
{
    Task SeedDefaultsForTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

