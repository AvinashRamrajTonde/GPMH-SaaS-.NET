using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Citizens;
using GPMH.Domain.Entities.Assessments;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class CitizenRepository(AppDbContext dbContext) : ICitizenRepository
{
    public async Task<PagedCitizensDto> SearchAsync(
        Guid tenantId,
        string? search,
        string? wardNo,
        string? exemptionCategory,
        string? exactAadhaarBlindIndex,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Citizen> query = dbContext.Citizens
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(exactAadhaarBlindIndex))
        {
            query = query.Where(c => c.AadhaarBlindIndex == exactAadhaarBlindIndex);
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            string normalized = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.CitizenCode.ToLower().Contains(normalized) ||
                c.FullNameMr.ToLower().Contains(normalized) ||
                c.FullNameEn.ToLower().Contains(normalized) ||
                (c.MobileNo != null && c.MobileNo.Contains(normalized)));
        }

        if (!string.IsNullOrWhiteSpace(wardNo))
        {
            string normalizedWard = wardNo.Trim().ToLowerInvariant();
            query = query.Where(c => c.WardNo != null && c.WardNo.ToLower() == normalizedWard);
        }

        if (!string.IsNullOrWhiteSpace(exemptionCategory))
        {
            string normalizedExemption = exemptionCategory.Trim().ToLowerInvariant();
            query = query.Where(c => c.ExemptionCategory.ToLower() == normalizedExemption);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<Citizen> rows = await query
            .OrderBy(c => c.CitizenCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> citizenIds = [.. rows.Select(c => c.Id)];
        Dictionary<Guid, int> linkedCounts = citizenIds.Count == 0
            ? []
            : await dbContext.PropertyOwners
                .AsNoTracking()
                .Where(po => po.IsActive && citizenIds.Contains(po.CitizenId))
                .GroupBy(po => po.CitizenId)
                .Select(g => new { CitizenId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CitizenId, x => x.Count, cancellationToken)
                .ConfigureAwait(false);

        List<CitizenDto> items = [.. rows.Select(c => new CitizenDto(
            c.Id,
            c.TenantId,
            c.CitizenCode,
            c.WardNo,
            c.FullNameMr,
            c.FullNameEn,
            c.Gender,
            c.MobileNo,
            string.IsNullOrWhiteSpace(c.AadhaarLast4) ? null : $"XXXX-XXXX-{c.AadhaarLast4}",
            c.SocialCategory,
            c.ExemptionCategory,
            c.IsActive,
            linkedCounts.GetValueOrDefault(c.Id),
            c.CreatedAtUtc,
            c.UpdatedAtUtc))];

        return new PagedCitizensDto(items, totalCount, page, pageSize);
    }

    public Task<Citizen?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Citizens.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Citizen>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Citizen>();
        }

        List<Citizen> citizens = await dbContext.Citizens
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return citizens.ToDictionary(c => c.Id);
    }

    public Task<bool> ExistsCitizenCodeAsync(
        string citizenCode,
        Guid? excludeCitizenId,
        CancellationToken cancellationToken) =>
        dbContext.Citizens.AnyAsync(
            c => c.CitizenCode == citizenCode && (!excludeCitizenId.HasValue || c.Id != excludeCitizenId.Value),
            cancellationToken);

    public Task<bool> ExistsAadhaarBlindIndexAsync(
        string blindIndex,
        Guid? excludeCitizenId,
        CancellationToken cancellationToken) =>
        dbContext.Citizens.AnyAsync(
            c => c.AadhaarBlindIndex == blindIndex && (!excludeCitizenId.HasValue || c.Id != excludeCitizenId.Value),
            cancellationToken);

    public async Task<string> GenerateNextCitizenCodeAsync(CancellationToken cancellationToken)
    {
        List<string> codes = await dbContext.Citizens
            .AsNoTracking()
            .Select(c => c.CitizenCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int maxNum = 0;
        foreach (string code in codes)
        {
            if (code.StartsWith("CIT-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(4), out int parsed) &&
                parsed > maxNum)
            {
                maxNum = parsed;
            }
        }

        return $"CIT-{maxNum + 1:D4}";
    }

    public Task<int> GetLinkedPropertiesCountAsync(Guid citizenId, CancellationToken cancellationToken) =>
        dbContext.PropertyOwners
            .AsNoTracking()
            .CountAsync(po => po.CitizenId == citizenId && po.IsActive, cancellationToken);

    public async Task AddAsync(Citizen citizen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(citizen);
        await dbContext.Citizens.AddAsync(citizen, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

