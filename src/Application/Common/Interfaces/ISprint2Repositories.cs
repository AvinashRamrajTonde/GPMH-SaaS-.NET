using GPMH.Application.DTOs.Agents;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;

namespace GPMH.Application.Common.Interfaces;

public sealed record TenantLicenseState(
    bool Exists,
    bool IsDeleted,
    TenantStatus Status,
    DateTimeOffset? SubscriptionExpiresAtUtc);

public interface ITenantLicenseChecker
{
    Task<TenantLicenseState?> GetLicenseStateAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IPlatformBillingRepository
{
    Task<PlatformPaymentAccount> GetOrCreatePlatformAccountAsync(CancellationToken cancellationToken);

    Task<PlatformPaymentAccount> UpdatePlatformAccountAsync(
        string accountName,
        string bankName,
        string branchName,
        string accountNumber,
        string ifscCode,
        string upiId,
        decimal annualFee,
        decimal gstPercent,
        string? gstin,
        string? supportPhone,
        string? supportEmail,
        CancellationToken cancellationToken);

    Task<GpSubscriptionStatusDto> GetGpSubscriptionStatusAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> ExistsNonRejectedUtrAsync(
        string utrReference,
        Guid? excludeSubmissionId,
        CancellationToken cancellationToken);

    Task<bool> ExistsSlipAssetInTenantAsync(Guid tenantId, string assetKey, CancellationToken cancellationToken);

    Task AddPaymentSubmissionAsync(
        TenantPaymentSubmission submission,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    Task<string> GenerateNextInvoiceNumberAsync(int year, CancellationToken cancellationToken);

    Task<GstSubscriptionInvoicePdfModel?> BuildGstInvoiceModelAsync(
        Guid submissionOrPaymentId,
        Guid? restrictToTenantId,
        CancellationToken cancellationToken);
}

public interface IOnboardingRepository
{
    Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<GramPanchayat?> GetGramPanchayatAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<AssessmentYear?> GetActiveAssessmentYearAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> HasFormulaSettingsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> ExistsTenantWithLgdAsync(string lgdCode, Guid? excludeTenantId, CancellationToken cancellationToken);

    Task<bool> ExistsUsernameAsync(string username, CancellationToken cancellationToken);

    Task AddGramPanchayatAsync(GramPanchayat gp, CancellationToken cancellationToken);

    Task AddAssessmentYearAsync(AssessmentYear year, CancellationToken cancellationToken);

    Task UpsertBankAccountsAsync(Guid tenantId, IReadOnlyList<BankAccount> accounts, CancellationToken cancellationToken);

    Task AddProtectedAssetAsync(ProtectedAsset asset, CancellationToken cancellationToken);

    Task<ProtectedAsset?> GetProtectedAssetAsync(string assetKey, CancellationToken cancellationToken);

    Task RegisterSelfServiceTenantAsync(
        Tenant tenant,
        TenantDatabaseMapping mapping,
        GramPanchayat gp,
        AssessmentYear year,
        User adminUser,
        TenantMembership membership,
        IReadOnlyList<BankAccount> bankAccounts,
        bool seedFormulaDefaults,
        CancellationToken cancellationToken);

    Task CompleteTenantOnboardingAsync(
        Guid tenantId,
        bool seedFormulaDefaults,
        CancellationToken cancellationToken);

    Task<(Guid AgentId, string AgentCode)?> FindApprovedAgentByCodeAsync(
        string referralCode,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDashboardReadRepository
{
    Task<DashboardSummaryDto> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<DashboardSearchResultDto> SearchTenantRecordsAsync(
        Guid tenantId,
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public interface ISuperAdminProvisioningRepository
{
    Task<IReadOnlyList<SuperAdminTenantSummaryDto>> ListTenantsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SuperAdminTenantSummaryDto>> ListTenantsFilteredAsync(
        string? search,
        string? district,
        string? taluka,
        string? status,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<SuperAdminDashboardStatsDto> GetStatewideStatsAsync(CancellationToken cancellationToken);

    Task<SuperAdminTenantDetailDto?> GetTenantDetailAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Tenant?> FindTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Tenant?> FindActiveTenantForImpersonationAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<(User User, TenantMembership Membership)?> FindTenantAdminUserAsync(
        Guid tenantId,
        string? targetUsername,
        CancellationToken cancellationToken);

    Task RevokeAllUserRefreshTokensAsync(
        Guid userId,
        string reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantPaymentSubmissionDto>> ListPaymentSubmissionsAsync(
        string? status,
        Guid? tenantId,
        string? search,
        CancellationToken cancellationToken);

    Task<TenantPaymentSubmission?> FindPaymentSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken);

    Task AddSubscriptionPaymentAsync(TenantSubscriptionPayment payment, CancellationToken cancellationToken);

    Task<IReadOnlyList<SuperAdminAuditLogItemDto>> ListAuditLogsAsync(
        Guid? tenantId,
        string? action,
        string? entityName,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    Task PermanentDeleteTenantAsync(Tenant tenant, AuditLog deletionAuditLog, CancellationToken cancellationToken);

    Task<bool> ExistsTenantLgdOrUsernameAsync(string lgdCode, string username, CancellationToken cancellationToken);

    Task ProvisionDirectTenantAsync(
        Tenant tenant,
        TenantDatabaseMapping mapping,
        GramPanchayat gp,
        AssessmentYear assessmentYear,
        User adminUser,
        TenantMembership membership,
        TenantSubscriptionPayment payment,
        CancellationToken cancellationToken);
}

public interface IAgentRepository
{
    Task<bool> ExistsDuplicateAsync(
        string usernameOrMobile,
        string aadhaarBlindIndex,
        string panBlindIndex,
        CancellationToken cancellationToken);

    Task RegisterAgentAsync(
        User user,
        FieldAgent agent,
        IReadOnlyList<ProtectedAsset> kycAssets,
        CancellationToken cancellationToken);

    Task<FieldAgent?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<FieldAgent?> FindByIdAsync(Guid agentId, CancellationToken cancellationToken);

    Task<FieldAgent?> FindByAgentCodeAsync(string agentCode, CancellationToken cancellationToken);

    Task<User?> FindUserByLoginAsync(string loginIdentifier, CancellationToken cancellationToken);

    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FieldAgent>> ListAllAsync(CancellationToken cancellationToken);

    Task<AgentDashboardDto> GetAgentDashboardAsync(Guid agentId, CancellationToken cancellationToken);

    Task<AgentVillagesResponseDto> GetAgentVillagesAsync(Guid agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentCommissionItemDto>> ListAgentCommissionsAsync(
        Guid agentId,
        string? status,
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentPayoutItemDto>> ListAgentPayoutsAsync(Guid agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SuperAdminAgentSummaryDto>> ListSuperAdminAgentDirectoryAsync(
        string? district,
        string? taluka,
        string? status,
        string? search,
        CancellationToken cancellationToken);

    Task<SuperAdminAgentSettlementsDto> GetSuperAdminSettlementsAsync(CancellationToken cancellationToken);

    Task<decimal> GetAvailableRequestableCommissionAsync(Guid agentId, CancellationToken cancellationToken);

    Task AddAgentPayoutAsync(AgentPayout payout, CancellationToken cancellationToken);

    Task<AgentCommission?> TryAccrueCommissionForPaymentAsync(
        Tenant tenant,
        TenantSubscriptionPayment payment,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string? ipAddress,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<AgentPayoutItemDto> SettleAgentPayoutAsync(
        Guid agentId,
        decimal amount,
        string paymentMode,
        string utrReference,
        DateOnly paymentDate,
        Guid? fulfillRequestedPayoutId,
        string? receiptAssetKey,
        string? remarks,
        Guid actorUserId,
        ApplicationRole? actorRole,
        string? ipAddress,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<AgentPayoutVoucherPdfModel?> BuildPayoutVoucherModelAsync(
        Guid payoutId,
        Guid? restrictToAgentId,
        CancellationToken cancellationToken);

    Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken);

    Task RevokeAllUserRefreshTokensAsync(
        Guid userId,
        string reason,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

