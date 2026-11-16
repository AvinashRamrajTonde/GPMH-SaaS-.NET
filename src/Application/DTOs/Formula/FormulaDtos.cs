using GPMH.Domain.Entities.Taxation;

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
    FormulaWorkspaceSummaryDto Summary);

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
}

