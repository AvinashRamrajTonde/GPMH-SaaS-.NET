using GPMH.Application.DTOs.Namuna9;
using GPMH.Domain.Enums;
using GPMH.Domain.ValueObjects;

namespace GPMH.Application.DTOs.Namuna10;

public sealed record TaxHeadDuesDto(
    decimal HouseTax,
    decimal LightingTax,
    decimal HealthTax,
    decimal SanitationTax,
    decimal ToiletTax,
    decimal GeneralWaterTax,
    decimal SpecialWaterTax,
    decimal OtherTax,
    decimal GeneralTaxTotal,
    decimal WaterTotal,
    decimal Total);

public sealed record Namuna10PropertySummaryReceiptDto(
    Guid ReceiptId,
    string ReceiptNo,
    string BookNo,
    DateOnly ReceiptDate,
    PaymentMode PaymentMode,
    decimal TotalPaid,
    bool IsCancelled);

public sealed record Namuna10RegisterRowDto(
    Guid DemandId,
    Guid PropertyId,
    string WardNo,
    string PropertyNo,
    string? PrimaryOwnerNameMr,
    string? PrimaryOwnerNameEn,
    string? PrimaryOwnerAadhaarMasked,
    string? PrimaryOwnerMobileMasked,
    string WaterConnectionCode,
    bool IsTaxExempt,
    string DemandStatus,
    string? ActiveBillNo,
    Guid? ActiveBillId,
    TaxHeadDuesDto ArrearsDue,
    TaxHeadDuesDto CurrentDue,
    decimal NoticeFeeDue,
    decimal WarrantFeeDue,
    decimal PenaltyDue,
    decimal CarriedFeesDue,
    decimal RebateAmount,
    bool EarlyRebateEligible,
    bool EarlyRebateApplied,
    DateOnly? RebateCutoffDate,
    decimal NetDemand,
    decimal PaidAmount,
    decimal RemainingBalance,
    Namuna10PropertySummaryReceiptDto? LastReceipt);

public sealed record Namuna10KpisDto(
    int TotalProperties,
    decimal TotalDemand,
    decimal TotalCollected,
    decimal TotalBalance,
    int PartiallyPaidCount,
    int FullyPaidCount);

public sealed record Namuna10RegisterWorkspaceDto(
    Namuna9YearDto SelectedYear,
    IReadOnlyList<Namuna9YearDto> AssessmentYears,
    int NoticeGraceDays,
    decimal EarlyRebatePct,
    DateOnly DefaultRebateCutoffDate,
    Namuna10KpisDto Kpis,
    IReadOnlyList<Namuna10RegisterRowDto> Items);

public sealed record TaxReceiptDto(
    Guid ReceiptId,
    Guid TenantId,
    Guid PropertyId,
    Guid PropertyDemandId,
    Guid AssessmentYearId,
    string YearLabel,
    Guid OperationId,
    Guid? DemandBillId,
    string? BillNo,
    string WardNo,
    string PropertyNo,
    string? PrimaryOwnerNameMr,
    string? PrimaryOwnerNameEn,
    string? PrimaryOwnerAadhaarMasked,
    string? PrimaryOwnerMobileMasked,
    string BookNo,
    string ReceiptNo,
    long ReceiptSequence,
    DateOnly ReceiptDate,
    DateTimeOffset CollectedAtUtc,
    PaymentMode PaymentMode,
    string PayerName,
    string? ReferenceNo,
    string? BankName,
    DateOnly? ChequeDate,
    string? Remarks,
    decimal TotalPaid,
    decimal DiscountGiven,
    decimal ExcessCredit,
    decimal RemainingBalanceAfterPayment,
    TaxReceiptHeadAllocation HeadAllocation,
    string AmountInWordsMr,
    bool IsCancelled,
    string? CancelReason,
    DateTimeOffset? CancelledAtUtc,
    bool IdempotentReplay = false);

public sealed record Namuna10ReceiptsPageDto(
    Namuna9YearDto SelectedYear,
    IReadOnlyList<Namuna9YearDto> AssessmentYears,
    int TotalCount,
    decimal TotalCollectedAmount,
    int CancelledCount,
    IReadOnlyList<TaxReceiptDto> Items);

