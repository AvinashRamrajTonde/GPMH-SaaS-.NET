using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Services;

namespace GPMH.Application.DTOs.Formula;

public sealed record FormulaSettingDto(
    Guid Id,
    Guid TenantId,
    string RatePeriodName,
    int RateStartYear,
    int RateEndYear,
    decimal TaxDivisor,
    int NoticeGraceDays,
    decimal EarlyRebatePct,
    decimal LatePenaltyPct,
    bool ApplyLightingHealthOnOpenPlot,
    bool RoundArea,
    bool IsActive,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record ConstructionTypeMasterDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string TypeName,
    string Category,
    decimal LandAnnualValueRate,
    decimal ConstructionReadyReckonerRate,
    decimal UsageWeightageFactor,
    decimal TaxRate,
    bool IsActive,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record UsageWeightageDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string UsageName,
    decimal Weightage,
    bool IsActive,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record DepreciationSlabDto(
    Guid Id,
    Guid TenantId,
    int AgeFrom,
    int AgeTo,
    bool FromInclusive,
    bool ToInclusive,
    decimal HutMudPercent,
    decimal StoneBrickRccPercent,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record AreaSlabTaxDto(
    Guid Id,
    Guid TenantId,
    int AreaFrom,
    int AreaTo,
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record WaterTaxMasterDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string WaterTypeName,
    decimal Amount,
    bool IsActive,
    string? LastResolutionNo,
    DateOnly? LastResolutionDate,
    string? LastChangeReason,
    DateTimeOffset? UpdatedAtUtc);

public sealed record FormulaWorkspaceSummaryDto(
    int ConstructionTypesCount,
    int UsageWeightagesCount,
    int DepreciationSlabsCount,
    int AreaSlabTaxesCount,
    int WaterTaxesCount,
    int RatePeriodsCount);

public sealed record FormulaWorkspaceDto(
    FormulaSettingDto? ActiveSetting,
    IReadOnlyList<FormulaSettingDto> RatePeriods,
    IReadOnlyList<ConstructionTypeMasterDto> ConstructionTypes,
    FormulaWorkspaceSummaryDto Summary,
    IReadOnlyList<UsageWeightageDto>? UsageWeightages = null,
    IReadOnlyList<DepreciationSlabDto>? DepreciationSlabs = null,
    IReadOnlyList<AreaSlabTaxDto>? AreaSlabTaxes = null,
    IReadOnlyList<WaterTaxMasterDto>? WaterTaxes = null);

public sealed record CalculatedPropertyTaxDto(
    int AssessmentStartYear,
    string RatePeriodName,
    decimal TaxDivisor,
    decimal TotalBuiltUpSqFt,
    decimal TotalBuiltUpSqM,
    decimal OpenAreaSqFt,
    decimal OpenAreaSqM,
    string WaterConnectionCode,
    string AppliedAreaSlabRange,
    PropertyTaxCalculationResult Calculation);

internal static class FormulaMappingExtensions
{
    public static FormulaSettingDto ToDto(this FormulaSetting entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.RatePeriodName,
            entity.RateStartYear,
            entity.RateEndYear,
            entity.TaxDivisor,
            entity.NoticeGraceDays,
            entity.EarlyRebatePct,
            entity.LatePenaltyPct,
            entity.ApplyLightingHealthOnOpenPlot,
            entity.RoundArea,
            entity.IsActive,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);

    public static ConstructionTypeMasterDto ToDto(this ConstructionTypeMaster entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.TypeName,
            entity.Category,
            entity.LandAnnualValueRate,
            entity.ConstructionReadyReckonerRate,
            entity.UsageWeightageFactor,
            entity.TaxRate,
            entity.IsActive,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);

    public static UsageWeightageDto ToDto(this UsageWeightage entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.UsageName,
            entity.Weightage,
            entity.IsActive,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);

    public static DepreciationSlabDto ToDto(this DepreciationSlab entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AgeFrom,
            entity.AgeTo,
            entity.FromInclusive,
            entity.ToInclusive,
            entity.HutMudPercent,
            entity.StoneBrickRccPercent,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);

    public static AreaSlabTaxDto ToDto(this AreaSlabTax entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AreaFrom,
            entity.AreaTo,
            entity.LightingTax,
            entity.HealthTax,
            entity.SanitationTax,
            entity.ToiletTax,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);

    public static WaterTaxMasterDto ToDto(this WaterTaxMaster entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.WaterTypeName,
            entity.Amount,
            entity.IsActive,
            entity.LastResolutionNo,
            entity.LastResolutionDate,
            entity.LastChangeReason,
            entity.UpdatedAtUtc);
}
