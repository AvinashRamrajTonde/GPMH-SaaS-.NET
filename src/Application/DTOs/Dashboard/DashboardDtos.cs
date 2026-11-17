namespace GPMH.Application.DTOs.Dashboard;

public sealed record SetupChecklistItemDto(
    int StepNumber,
    string Key,
    bool IsCompleted,
    string? Detail);

public sealed record DashboardActivityItemDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string? ActorId,
    string? ActorRole,
    DateTimeOffset TimestampUtc);

public sealed record PlatformPaymentAccountSummaryDto(
    string AccountName,
    string BankName,
    string BranchName,
    string AccountNumber,
    string IfscCode,
    string UpiId,
    decimal AnnualFee,
    decimal GstPercent,
    string? Gstin,
    string? SupportPhone,
    string? SupportEmail);

public sealed record DashboardSummaryDto(
    Guid TenantId,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string LgdCode,
    string ActiveAssessmentYear,
    int SetupProgressPct,
    int TotalProperties,
    decimal CurrentFyDemand,
    decimal TodayCollection,
    decimal MonthCollection,
    decimal OutstandingArrears,
    int PendingMutations,
    decimal WaterAndSpecialCollection,
    int TodayReceipts,
    IReadOnlyList<SetupChecklistItemDto> ChecklistSteps,
    IReadOnlyList<DashboardActivityItemDto> RecentActivity,
    string SubscriptionStatus = "Active",
    DateTimeOffset? SubscriptionExpiresAtUtc = null,
    int? DaysUntilExpiry = null,
    bool ShowExpiryWarning30Days = false,
    decimal AnnualRenewalFee = 6000.00m,
    PlatformPaymentAccountSummaryDto? PlatformPaymentInfo = null);

public sealed record DashboardPropertyHitDto(
    Guid Id,
    string WardNo,
    string PropertyNo,
    decimal TotalAreaSqFt,
    string WaterConnectionCode);

public sealed record DashboardCitizenHitDto(
    Guid Id,
    string CitizenCode,
    string? WardNo,
    string FullNameEn,
    string FullNameMr,
    string? MobileNo,
    string? AadhaarMasked);

public sealed record DashboardSearchResultDto(
    IReadOnlyList<DashboardPropertyHitDto> Properties,
    IReadOnlyList<DashboardCitizenHitDto> Citizens);

