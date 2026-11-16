using GPMH.Infrastructure.Security;
using GPMH.Infrastructure.Taxation;
using Microsoft.Extensions.Options;
using Xunit;

namespace GPMH.UnitTests;

public sealed class DefaultFormulaSeederAndPiiTests
{
    [Fact]
    public void DefaultFormulaSeeder_ProducesCanonicalAdr016SeedCountsAndGoldenRates()
    {
        Guid tenantId = Guid.NewGuid();

        var constructions = DefaultFormulaSeeder.CreateConstructionTypes(tenantId);
        var weights = DefaultFormulaSeeder.CreateUsageWeightages(tenantId);
        var slabs = DefaultFormulaSeeder.CreateDepreciationSlabs(tenantId);
        var areaSlabs = DefaultFormulaSeeder.CreateAreaSlabTaxes(tenantId);
        var waterTaxes = DefaultFormulaSeeder.CreateWaterTaxes(tenantId);

        Assert.Equal(12, constructions.Count);
        Assert.Equal(3, weights.Count);
        Assert.Equal(9, slabs.Count);
        Assert.Equal(3, areaSlabs.Count);
        Assert.Equal(3, waterTaxes.Count);

        var hutMud = Assert.Single(constructions, c => c.Code == "HUT_MUD");
        Assert.Equal(760m, hutMud.LandAnnualValueRate);
        Assert.Equal(5852m, hutMud.ConstructionReadyReckonerRate);
        Assert.Equal(0.5m, hutMud.TaxRate);

        var commercial = Assert.Single(weights, w => w.Code == "COMMERCIAL");
        Assert.Equal(1.25m, commercial.Weightage);

        var slab2 = Assert.Single(slabs, s => s.AgeFrom == 2 && s.AgeTo == 5);
        Assert.False(slab2.FromInclusive);
        Assert.True(slab2.ToInclusive);
        Assert.Equal(95m, slab2.HutMudPercent);
    }

    [Fact]
    public void AesGcmPiiProtectionService_EncryptsWithUniqueNoncesAndDeterministicallyBlindIndexes()
    {
        var svc = new AesGcmPiiProtectionService(Options.Create(new SecurityOptions
        {
            PiiEncryptionKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
            BlindIndexKey = Convert.ToBase64String(Enumerable.Range(33, 32).Select(i => (byte)i).ToArray()),
        }));

        byte[] cipher1 = svc.EncryptString("432156789012");
        byte[] cipher2 = svc.EncryptString("432156789012");

        Assert.NotEqual(cipher1, cipher2);
        Assert.Equal("432156789012", svc.DecryptString(cipher1));
        Assert.Equal("432156789012", svc.DecryptString(cipher2));

        Assert.Equal(svc.ComputeBlindIndex("abcde1234f"), svc.ComputeBlindIndex("ABCDE1234F"));
        Assert.Equal("XXXX-XXXX-9012", svc.MaskAadhaar("4321 5678 9012"));
        Assert.Equal("AB***1234F", svc.MaskPan("abcde1234f"));
    }
}

