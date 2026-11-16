using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class OnboardingRepository(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDefaultFormulaSeeder formulaSeeder)
    : IOnboardingRepository
{
    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<GramPanchayat?> GetGramPanchayatAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.GramPanchayats.FirstOrDefaultAsync(gp => gp.TenantId == tenantId, cancellationToken);

    public Task<AssessmentYear?> GetActiveAssessmentYearAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.AssessmentYears
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.StartYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.BankAccounts
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.FundType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> HasFormulaSettingsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.FormulaSettings.AnyAsync(f => f.TenantId == tenantId && f.IsActive, cancellationToken);

    public Task<bool> ExistsTenantWithLgdAsync(string lgdCode, Guid? excludeTenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.AnyAsync(
            t => t.LgdCode == lgdCode && (!excludeTenantId.HasValue || t.Id != excludeTenantId.Value),
            cancellationToken);

    public Task<bool> ExistsUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public async Task AddGramPanchayatAsync(GramPanchayat gp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gp);
        await dbContext.GramPanchayats.AddAsync(gp, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAssessmentYearAsync(AssessmentYear year, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(year);
        await dbContext.AssessmentYears.AddAsync(year, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertBankAccountsAsync(
        Guid tenantId,
        IReadOnlyList<BankAccount> accounts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        List<BankAccount> existing = await dbContext.BankAccounts
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (BankAccount incoming in accounts)
        {
            BankAccount? match = existing.FirstOrDefault(
                b => string.Equals(b.FundType, incoming.FundType, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                await dbContext.BankAccounts.AddAsync(incoming, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                match.BankName = incoming.BankName;
                match.BranchName = incoming.BranchName;
                match.AccountNumber = incoming.AccountNumber;
                match.IfscCode = incoming.IfscCode;
                match.UpiId = incoming.UpiId;
                match.UpiPayeeName = incoming.UpiPayeeName;
                match.QrImageKey = incoming.QrImageKey ?? match.QrImageKey;
                match.OpeningBalance = incoming.OpeningBalance;
                match.IsDefault = incoming.IsDefault;
                match.IsActive = true;
            }
        }
    }

    public async Task AddProtectedAssetAsync(ProtectedAsset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        await dbContext.ProtectedAssets.AddAsync(asset, cancellationToken).ConfigureAwait(false);
    }

    public Task<ProtectedAsset?> GetProtectedAssetAsync(string assetKey, CancellationToken cancellationToken) =>
        dbContext.ProtectedAssets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetKey == assetKey, cancellationToken);

    public async Task RegisterSelfServiceTenantAsync(
        Tenant tenant,
        TenantDatabaseMapping mapping,
        GramPanchayat gp,
        AssessmentYear year,
        User adminUser,
        TenantMembership membership,
        IReadOnlyList<BankAccount> bankAccounts,
        bool seedFormulaDefaults,
        CancellationToken cancellationToken)
    {
        Guid? previousTenant = tenantContext.CurrentTenantId;
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            tenantContext.SetTenant(tenant.Id);

            await dbContext.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantDatabaseMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
            await dbContext.Users.AddAsync(adminUser, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
            await dbContext.GramPanchayats.AddAsync(gp, cancellationToken).ConfigureAwait(false);
            await dbContext.AssessmentYears.AddAsync(year, cancellationToken).ConfigureAwait(false);

            if (bankAccounts.Count > 0)
            {
                await dbContext.BankAccounts.AddRangeAsync(bankAccounts, cancellationToken).ConfigureAwait(false);
            }

            if (seedFormulaDefaults)
            {
                await formulaSeeder.SeedDefaultsForTenantAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (previousTenant is Guid prev)
            {
                tenantContext.SetTenant(prev);
            }
            else
            {
                tenantContext.Clear();
            }
        }
    }

    public async Task CompleteTenantOnboardingAsync(
        Guid tenantId,
        bool seedFormulaDefaults,
        CancellationToken cancellationToken)
    {
        if (seedFormulaDefaults)
        {
            await formulaSeeder.SeedDefaultsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

