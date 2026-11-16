namespace GPMH.Application.DTOs.Properties;

public sealed record PropertyDto(
    Guid Id,
    Guid TenantId,
    Guid? ParentPropertyId,
    string WardNo,
    string PropertyNo,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    decimal TotalAreaSqFt,
    string? BoundaryEast,
    string? BoundaryWest,
    string? BoundaryNorth,
    string? BoundarySouth,
    string WaterConnectionCode,
    bool IsTaxExempt,
    bool IsActive,
    string? StreetOrLaneName = null,
    string? CitySurveyOrGutNo = null,
    string? OccupierName = null,
    string? PropertyDescription = null,
    decimal OtherTax = 0m,
    string Status = "Active",
    string? PrimaryOwnerNameMr = null,
    string? PrimaryOwnerNameEn = null,
    decimal? TotalAnnualTax = null,
    decimal? PendingBalance = null,
    int FloorCount = 0);

public sealed record PropertyOwnerDetailDto(
    Guid Id,
    Guid CitizenId,
    string CitizenCode,
    string FullNameMr,
    string FullNameEn,
    string? MobileNo,
    string? AadhaarMasked,
    string ExemptionCategory,
    decimal OwnershipSharePct,
    bool IsPrimaryOwner,
    bool IsActive);

public sealed record PropertyConstructionDto(
    Guid Id,
    int FloorNo,
    string FloorLabel,
    string ConstructionTypeCode,
    string UsageCode,
    decimal LengthFt,
    decimal WidthFt,
    decimal AreaSqFt,
    decimal AreaSqM,
    int ConstructionStartYear,
    bool IsActive);

public sealed record PropertyTaxCalcDto(
    Guid Id,
    Guid AssessmentYearId,
    string AssessmentYearLabel,
    decimal BuildingTax,
    decimal OpenLandTax,
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax,
    decimal WaterTax,
    decimal OtherTax,
    decimal TotalTax,
    string CalculationSnapshotJson,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PropertyDemandDto(
    Guid Id,
    Guid AssessmentYearId,
    string AssessmentYearLabel,
    decimal ArrearsBalance,
    decimal CurrentDemand,
    decimal RebateAmount,
    decimal PenaltyAmount,
    decimal PaidAmount,
    decimal PendingBalance,
    string Status);

public sealed record ChildPropertySummaryDto(
    Guid Id,
    string WardNo,
    string PropertyNo,
    decimal TotalAreaSqFt,
    string Status);

public sealed record PropertyDetailDto(
    Guid Id,
    Guid TenantId,
    Guid? ParentPropertyId,
    string WardNo,
    string PropertyNo,
    string? StreetOrLaneName,
    string? CitySurveyOrGutNo,
    string? OccupierName,
    string? PropertyDescription,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    decimal TotalAreaSqFt,
    string? BoundaryEast,
    string? BoundaryWest,
    string? BoundaryNorth,
    string? BoundarySouth,
    string WaterConnectionCode,
    decimal OtherTax,
    bool IsTaxExempt,
    string Status,
    bool IsActive,
    IReadOnlyList<PropertyOwnerDetailDto> Owners,
    IReadOnlyList<PropertyConstructionDto> Constructions,
    PropertyTaxCalcDto? TaxCalc,
    PropertyDemandDto? Demand,
    IReadOnlyList<ChildPropertySummaryDto> ChildProperties);

public sealed record PropertyHubKpisDto(
    int TotalProperties,
    decimal TotalAssessedDemand,
    int ExemptPropertiesCount,
    int ActiveWardsCount,
    int PartitionedCount);
