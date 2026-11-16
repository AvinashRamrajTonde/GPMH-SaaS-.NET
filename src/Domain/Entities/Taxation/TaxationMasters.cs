using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Taxation;

public sealed class FormulaSetting : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string RatePeriodName { get; set; }
    public int RateStartYear { get; set; }
    public int RateEndYear { get; set; }
    public decimal TaxDivisor { get; set; } = 1000m;
    public int NoticeGraceDays { get; set; } = 15;
    public decimal EarlyRebatePct { get; set; } = 10m;
    public decimal LatePenaltyPct { get; set; } = 10m;
    public bool ApplyLightingHealthOnOpenPlot { get; set; } = true;
    public bool RoundArea { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? LastResolutionNo { get; set; }
    public DateOnly? LastResolutionDate { get; set; }
    public string? LastChangeReason { get; set; }
}

public sealed class ConstructionTypeMaster : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string TypeName { get; set; }
    public required string Category { get; set; }
    public decimal LandAnnualValueRate { get; set; }
    public decimal ConstructionReadyReckonerRate { get; set; }
    public decimal UsageWeightageFactor { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? LastResolutionNo { get; set; }
    public DateOnly? LastResolutionDate { get; set; }
    public string? LastChangeReason { get; set; }
}

public sealed class UsageWeightage : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string UsageName { get; set; }
    public decimal Weightage { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DepreciationSlab : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int AgeFrom { get; set; }
    public int AgeTo { get; set; }
    public bool FromInclusive { get; set; }
    public bool ToInclusive { get; set; } = true;
    public decimal HutMudPercent { get; set; }
    public decimal StoneBrickRccPercent { get; set; }
}

public sealed class AreaSlabTax : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int AreaFrom { get; set; }
    public int AreaTo { get; set; }
    public decimal LightingTax { get; set; }
    public decimal HealthTax { get; set; }
    public decimal SanitationTax { get; set; }
    public decimal ToiletTax { get; set; }
}

public sealed class WaterTaxMaster : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string WaterTypeName { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}

