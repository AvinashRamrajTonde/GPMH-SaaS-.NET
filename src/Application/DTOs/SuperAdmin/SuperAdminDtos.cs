namespace GPMH.Application.DTOs.SuperAdmin;

public sealed record ProvisionedTenantResultDto(
    Guid TenantId,
    string TenantCode,
    string LgdCode,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string Status,
    DateTimeOffset SubscriptionExpiresAtUtc,
    string AdminUsername,
    string AdminRole,
    string PaymentReference,
    decimal AmountPaid,
    bool FormulaSeeded);

public sealed record SuperAdminTenantSummaryDto(
    Guid TenantId,
    string TenantCode,
    string LgdCode,
    string PanchayatNameEn,
    string PanchayatNameMr,
    string DistrictName,
    string TehsilName,
    string Status,
    DateTimeOffset? SubscriptionExpiresAtUtc,
    int SetupProgressPct);

