using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;

namespace GPMH.Application.Common.Interfaces;

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

    Task<User?> FindUserByLoginAsync(string loginIdentifier, CancellationToken cancellationToken);

    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FieldAgent>> ListAllAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

