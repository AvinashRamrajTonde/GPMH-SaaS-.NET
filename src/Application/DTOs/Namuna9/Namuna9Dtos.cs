using GPMH.Domain.ValueObjects;

namespace GPMH.Application.DTOs.Namuna9;

/// <summary>Ma. (arrears) / Cha. (current) / E. (total) tri-column for one tax head.</summary>
public sealed record Namuna9HeadAmountsDto(decimal Arrears, decimal Current, decimal Total);

public sealed record Namuna9DemandRowDto(
    Guid DemandId,
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    string? PrimaryOwnerNameMr,
    string? PrimaryOwnerNameEn,
    string WaterConnectionCode,
    bool IsTaxExempt,
    // Page 1 — demand register (मागणी नोंदवही)
    Namuna9HeadAmountsDto HouseTax,
    Namuna9HeadAmountsDto LightingTax,
    Namuna9HeadAmountsDto HealthTax,
    Namuna9HeadAmountsDto SanitationTax,
    Namuna9HeadAmountsDto ToiletTax,
    Namuna9HeadAmountsDto GeneralWaterTax,
    Namuna9HeadAmountsDto SpecialWaterTax,
    Namuna9HeadAmountsDto OtherTax,
    decimal CarriedFeesAndPenalty,
    decimal NoticeFee,
    decimal WarrantFee,
    decimal PenaltyAmount,
    decimal RebateAmount,
    decimal TotalArrears,
    decimal TotalCurrent,
    decimal NetDemand,
    // Page 2 — recovery & balance register (वसुली व बाकी नोंदवही)
    DemandCollectedBreakdown CollectedByHead,
    DemandHeadBreakdown BalanceByHead,
    decimal PaidAmount,
    decimal PendingBalance,
    string Status,
    bool EarlyRebateApplied,
    DateOnly? RebateCutoffDate,
    string? Remarks,
    DateTimeOffset? PublishedAtUtc);

public sealed record Namuna9KpisDto(
    decimal TotalDemand,
    decimal TotalCollected,
    decimal TotalRebate,
    decimal TotalBalanceDues,
    decimal GeneralTaxDemand,
    decimal WaterTaxDemand,
    int PropertyCount);

public sealed record Namuna9YearDto(
    Guid Id,
    string YearLabel,
    int StartYear,
    int EndYear,
    bool IsActive,
    bool IsLocked);

public sealed record Namuna9FormulaRatesDto(
    string RatePeriodName,
    decimal TaxDivisor,
    int NoticeGraceDays,
    decimal EarlyRebatePct,
    decimal LatePenaltyPct,
    DateOnly DefaultRebateCutoffDate);

public sealed record Namuna9WorkspaceDto(
    Namuna9YearDto SelectedYear,
    IReadOnlyList<Namuna9YearDto> AssessmentYears,
    Namuna9FormulaRatesDto FormulaRates,
    string Format,
    Namuna9KpisDto Kpis,
    IReadOnlyList<Namuna9DemandRowDto> Items);

public sealed record Namuna9FeeAdjustmentItem(
    Guid PropertyId,
    decimal NoticeFee,
    decimal WarrantFee,
    decimal PenaltyAmount,
    decimal RebateAmount,
    string? Remarks = null);

public sealed record Namuna9PublishResultDto(
    string YearLabel,
    int PublishedCount,
    int SkippedCount);

public sealed record Namuna9AdjustedDemandDto(
    Guid DemandId,
    Guid PropertyId,
    decimal NetDemand,
    decimal PendingBalance,
    string Status);

public sealed record Namuna9AdjustResultDto(
    string YearLabel,
    int UpdatedCount,
    IReadOnlyList<Namuna9AdjustedDemandDto> Items);

public sealed record Namuna9RebateResultDto(
    string YearLabel,
    DateOnly ApplicationDate,
    DateOnly CutoffDate,
    decimal EarlyRebatePct,
    int AppliedCount,
    int SkippedCount,
    decimal TotalRebateGranted);

public sealed record Namuna9RolloverResultDto(
    Guid SourceYearId,
    string SourceYearLabel,
    Guid TargetYearId,
    string TargetYearLabel,
    int PropertiesRolledOver,
    decimal TotalCarriedArrears,
    decimal TotalCurrentDemand);

