using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class SuperAdminProvisioningRepository(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDefaultFormulaSeeder formulaSeeder)
    : ISuperAdminProvisioningRepository
{
    public async Task<IReadOnlyList<SuperAdminTenantSummaryDto>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        List<Tenant> tenants = await dbContext.Tenants
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<GramPanchayat> gps = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, GramPanchayat> gpByTenant = gps
            .GroupBy(g => g.TenantId)
            .ToDictionary(g => g.Key, g => g.First());

        return tenants
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t =>
            {
                gpByTenant.TryGetValue(t.Id, out GramPanchayat? gp);
                return new SuperAdminTenantSummaryDto(
                    t.Id,
                    t.TenantCode,
                    t.LgdCode,
                    t.NameEn,
                    t.NameMr,
                    gp?.DistrictName ?? string.Empty,
                    gp?.TehsilName ?? string.Empty,
                    t.Status.ToString(),
                    t.SubscriptionExpiresAtUtc,
                    gp?.SetupProgressPct ?? 0);
            })
            .ToList();
    }

    public async Task<bool> ExistsTenantLgdOrUsernameAsync(
        string lgdCode,
        string username,
        CancellationToken cancellationToken)
    {
        bool lgdExists = await dbContext.Tenants
            .AnyAsync(t => t.LgdCode == lgdCode, cancellationToken)
            .ConfigureAwait(false);

        if (lgdExists)
        {
            return true;
        }

        return await dbContext.Users
            .AnyAsync(u => u.Username == username, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ProvisionDirectTenantAsync(
        Tenant tenant,
        TenantDatabaseMapping mapping,
        GramPanchayat gp,
        AssessmentYear assessmentYear,
        User adminUser,
        TenantMembership membership,
        TenantSubscriptionPayment payment,
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
            await dbContext.AssessmentYears.AddAsync(assessmentYear, cancellationToken).ConfigureAwait(false);
            await dbContext.TenantSubscriptionPayments.AddAsync(payment, cancellationToken).ConfigureAwait(false);

            await formulaSeeder.SeedDefaultsForTenantAsync(tenant.Id, cancellationToken).ConfigureAwait(false);

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
}

