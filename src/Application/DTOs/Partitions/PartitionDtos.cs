namespace GPMH.Application.DTOs.Partitions;

public sealed record PartitionOwnerItemDto(
    Guid CitizenId,
    string FullNameMr,
    string FullNameEn,
    decimal OwnershipSharePct,
    bool IsPrimaryOwner);

public sealed record PartitionFloorItemDto(
    int FloorNo,
    string FloorLabel,
    string ConstructionTypeCode,
    string UsageCode,
    decimal LengthFt,
    decimal WidthFt,
    decimal AreaSqFt,
    decimal AreaSqM,
    int ConstructionStartYear);

public sealed record PartitionEligibilityDto(
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    string Status,
    bool IsActive,
    string? StreetOrLaneName,
    string? CitySurveyOrGutNo,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    decimal TotalAreaSqFt,
    decimal TotalAreaSqM,
    decimal PendingDues,
    decimal ArrearsBalance,
    decimal CurrentDemand,
    decimal PaidAmount,
    bool ZeroDuesGatePassed,
    string? BlockReason,
    string WaterConnectionCode,
    bool IsTaxExempt,
    decimal OtherTax,
    IReadOnlyList<PartitionOwnerItemDto> CurrentOwners,
    IReadOnlyList<PartitionFloorItemDto> CurrentFloors,
    IReadOnlyList<string> SuggestedChildPropertyNos);

public sealed record PartitionChildResultDto(
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    decimal BuiltUpAreaSqFt,
    decimal OpenPlotAreaSqFt,
    decimal TotalAreaSqFt,
    decimal AreaSqM,
    decimal TotalAnnualTax,
    decimal DraftDemandBalance,
    string PrimaryOwnerNameMr,
    string PrimaryOwnerNameEn);

public sealed record ExecutePartitionResultDto(
    Guid ParentPropertyId,
    string OriginalParentPropertyNo,
    string ArchivedParentPropertyNo,
    Guid PartitionMutationId,
    string ApplicationNo,
    decimal ParentAreaSqM,
    decimal TotalChildAreaSqM,
    decimal AreaDeltaSqM,
    decimal AllowedToleranceSqM,
    IReadOnlyList<PartitionChildResultDto> ChildProperties);

