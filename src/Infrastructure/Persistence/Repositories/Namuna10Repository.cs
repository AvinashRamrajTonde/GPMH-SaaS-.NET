using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Assessments;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Taxation;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>Tenant-scoped EF Core repository for Namuna 10 collection register, FIFO receipts, and atomic sequencing.</summary>
public sealed class Namuna10Repository(
    AppDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : INamuna10Repository
{
    public async Task<IReadOnlyList<AssessmentYear>> ListYearsAsync(CancellationToken cancellationToken)
    {
        List<AssessmentYear> years = await dbContext.AssessmentYears
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. years.OrderByDescending(y => y.StartYear).ThenBy(y => y.YearLabel, StringComparer.Ordinal)];
    }

    public async Task<AssessmentYear?> GetYearAsync(Guid? yearId, CancellationToken cancellationToken)
    {
        if (yearId.HasValue)
        {
            return await dbContext.AssessmentYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        List<AssessmentYear> activeYears = await dbContext.AssessmentYears
            .AsNoTracking()
            .Where(y => y.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return activeYears
            .OrderByDescending(y => y.StartYear)
            .FirstOrDefault();
    }

    public async Task<FormulaSetting?> GetActiveFormulaSettingAsync(CancellationToken cancellationToken)
    {
        List<FormulaSetting> active = await dbContext.FormulaSettings
            .AsNoTracking()
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return active
            .OrderByDescending(f => f.RateStartYear)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<Namuna10DemandContext>> LoadRegisterContextsAsync(
        Guid assessmentYearId,
        string? wardNo,
        string? search,
        CancellationToken cancellationToken)
    {
        List<PropertyDemand> demands = await dbContext.PropertyDemands
            .AsNoTracking()
            .Where(d => d.AssessmentYearId == assessmentYearId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (demands.Count == 0)
        {
            return [];
        }

        List<Guid> propIds = [.. demands.Select(d => d.PropertyId).Distinct()];

        Dictionary<Guid, Property> propsById = await dbContext.Properties
            .AsNoTracking()
            .Where(p => propIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, OwnerSummary> ownersByProp = await LoadPrimaryOwnersAsync(propIds, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyTaxCalc> calcsByProp = await dbContext.PropertyTaxCalcs
            .AsNoTracking()
            .Where(c => c.AssessmentYearId == assessmentYearId && propIds.Contains(c.PropertyId))
            .ToDictionaryAsync(c => c.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        List<PropertyDemandBill> billsRaw = await dbContext.PropertyDemandBills
            .AsNoTracking()
            .Where(b => b.AssessmentYearId == assessmentYearId &&
                        b.Status != PropertyDemandBill.StatusSuperseded &&
                        propIds.Contains(b.PropertyId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemandBill> activeBillsByProp = billsRaw
            .GroupBy(b => b.PropertyId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.BillSequence).First());

        List<TaxReceipt> receiptsRaw = await dbContext.TaxReceipts
            .AsNoTracking()
            .Where(r => r.AssessmentYearId == assessmentYearId && propIds.Contains(r.PropertyId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, TaxReceipt> lastReceiptsByProp = receiptsRaw
            .GroupBy(r => r.PropertyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.IsCancelled ? 1 : 0).ThenByDescending(r => r.ReceiptSequence).First());

        string? normalizedWard = string.IsNullOrWhiteSpace(wardNo) ? null : wardNo.Trim();
        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        List<Namuna10DemandContext> results = [];

        foreach (PropertyDemand demand in demands)
        {
            if (!propsById.TryGetValue(demand.PropertyId, out Property? prop))
            {
                continue;
            }

            if (normalizedWard is not null &&
                !string.Equals(prop.WardNo, normalizedWard, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ownersByProp.TryGetValue(prop.Id, out OwnerSummary? owner);
            calcsByProp.TryGetValue(prop.Id, out PropertyTaxCalc? taxCalc);
            activeBillsByProp.TryGetValue(prop.Id, out PropertyDemandBill? activeBill);
            lastReceiptsByProp.TryGetValue(prop.Id, out TaxReceipt? lastReceipt);

            if (normalizedSearch is not null &&
                !prop.PropertyNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                !prop.WardNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                (owner?.NameMr is null || !owner.NameMr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) &&
                (owner?.NameEn is null || !owner.NameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) &&
                (owner?.RawMobile is null || !owner.RawMobile.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) &&
                (activeBill?.BillNo is null || !activeBill.BillNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            results.Add(new Namuna10DemandContext(
                Demand: demand,
                Property: prop,
                TaxCalc: taxCalc,
                OwnerNameMr: owner?.NameMr,
                OwnerNameEn: owner?.NameEn,
                OwnerAadhaarMasked: owner?.AadhaarMasked,
                OwnerMobileMasked: owner?.MobileMasked,
                ActiveBill: activeBill,
                LastReceipt: lastReceipt));
        }

        return
        [
            .. results
                .OrderBy(r => r.Property.WardNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Property.PropertyNo, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<Namuna10ReceiptContext?> FindReceiptByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        TaxReceipt? receipt = await dbContext.TaxReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OperationId == operationId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return null;
        }

        return await HydrateReceiptContextAsync(receipt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Namuna10CollectionContext?> LoadCollectionContextAsync(
        Guid propertyId,
        Guid assessmentYearId,
        CancellationToken cancellationToken)
    {
        AssessmentYear? year = await dbContext.AssessmentYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == assessmentYearId, cancellationToken)
            .ConfigureAwait(false);
        if (year is null)
        {
            return null;
        }

        Property? prop = await dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);
        if (prop is null)
        {
            return null;
        }

        PropertyDemand? demand = await dbContext.PropertyDemands
            .FirstOrDefaultAsync(
                d => d.PropertyId == propertyId && d.AssessmentYearId == assessmentYearId,
                cancellationToken)
            .ConfigureAwait(false);
        if (demand is null)
        {
            return null;
        }

        FormulaSetting? formula = await GetActiveFormulaSettingAsync(cancellationToken).ConfigureAwait(false);

        PropertyTaxCalc? taxCalc = await dbContext.PropertyTaxCalcs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.PropertyId == propertyId && c.AssessmentYearId == assessmentYearId,
                cancellationToken)
            .ConfigureAwait(false);

        List<PropertyDemandBill> bills = await dbContext.PropertyDemandBills
            .Where(b => b.PropertyId == propertyId &&
                        b.AssessmentYearId == assessmentYearId &&
                        b.Status != PropertyDemandBill.StatusSuperseded)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        PropertyDemandBill? activeBill = bills.OrderByDescending(b => b.BillSequence).FirstOrDefault();

        Dictionary<Guid, OwnerSummary> owners = await LoadPrimaryOwnersAsync([propertyId], cancellationToken)
            .ConfigureAwait(false);
        owners.TryGetValue(propertyId, out OwnerSummary? owner);

        return new Namuna10CollectionContext(
            Year: year,
            Formula: formula,
            Property: prop,
            Demand: demand,
            TaxCalc: taxCalc,
            ActiveBill: activeBill,
            DefaultOwnerNameMr: owner?.NameMr,
            DefaultOwnerNameEn: owner?.NameEn,
            OwnerAadhaarMasked: owner?.AadhaarMasked,
            OwnerMobileMasked: owner?.MobileMasked);
    }

    public async Task<IReadOnlyList<Namuna10ReceiptContext>> ListReceiptsAsync(
        Guid? assessmentYearId,
        Guid? propertyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        PaymentMode? mode,
        string? wardNo,
        string? search,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        IQueryable<TaxReceipt> query = dbContext.TaxReceipts.AsNoTracking();

        if (assessmentYearId.HasValue)
        {
            query = query.Where(r => r.AssessmentYearId == assessmentYearId.Value);
        }

        if (propertyId.HasValue)
        {
            query = query.Where(r => r.PropertyId == propertyId.Value);
        }

        if (!includeCancelled)
        {
            query = query.Where(r => !r.IsCancelled);
        }

        if (mode.HasValue)
        {
            query = query.Where(r => r.PaymentMode == mode.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.ReceiptDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.ReceiptDate <= toDate.Value);
        }

        List<TaxReceipt> receipts = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (receipts.Count == 0)
        {
            return [];
        }

        List<Guid> yearIds = [.. receipts.Select(r => r.AssessmentYearId).Distinct()];
        List<Guid> propIds = [.. receipts.Select(r => r.PropertyId).Distinct()];
        List<Guid> demandIds = [.. receipts.Select(r => r.PropertyDemandId).Distinct()];
        List<Guid> billIds = [.. receipts.Where(r => r.DemandBillId.HasValue).Select(r => r.DemandBillId!.Value).Distinct()];

        Dictionary<Guid, AssessmentYear> yearsById = await dbContext.AssessmentYears
            .AsNoTracking()
            .Where(y => yearIds.Contains(y.Id))
            .ToDictionaryAsync(y => y.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, Property> propsById = await dbContext.Properties
            .AsNoTracking()
            .Where(p => propIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemand> demandsById = await dbContext.PropertyDemands
            .AsNoTracking()
            .Where(d => demandIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, PropertyDemandBill> billsById = billIds.Count > 0
            ? await dbContext.PropertyDemandBills
                .AsNoTracking()
                .Where(b => billIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, cancellationToken)
                .ConfigureAwait(false)
            : [];

        Dictionary<Guid, OwnerSummary> ownersByProp = await LoadPrimaryOwnersAsync(propIds, cancellationToken)
            .ConfigureAwait(false);

        string? normalizedWard = string.IsNullOrWhiteSpace(wardNo) ? null : wardNo.Trim();
        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        List<Namuna10ReceiptContext> results = [];

        foreach (TaxReceipt r in receipts.OrderByDescending(x => x.ReceiptDate).ThenByDescending(x => x.ReceiptSequence))
        {
            if (!yearsById.TryGetValue(r.AssessmentYearId, out AssessmentYear? yr) ||
                !propsById.TryGetValue(r.PropertyId, out Property? prop) ||
                !demandsById.TryGetValue(r.PropertyDemandId, out PropertyDemand? dem))
            {
                continue;
            }

            if (normalizedWard is not null &&
                !string.Equals(prop.WardNo, normalizedWard, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ownersByProp.TryGetValue(prop.Id, out OwnerSummary? owner);
            PropertyDemandBill? bill = r.DemandBillId.HasValue && billsById.TryGetValue(r.DemandBillId.Value, out PropertyDemandBill? b)
                ? b
                : null;

            if (normalizedSearch is not null &&
                !r.ReceiptNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                !r.BookNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                !r.PayerName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                !prop.PropertyNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
                (r.ReferenceNo is null || !r.ReferenceNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) &&
                (owner?.NameMr is null || !owner.NameMr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) &&
                (owner?.NameEn is null || !owner.NameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            results.Add(new Namuna10ReceiptContext(
                Receipt: r,
                Year: yr,
                Property: prop,
                Demand: dem,
                Bill: bill,
                OwnerNameMr: owner?.NameMr,
                OwnerNameEn: owner?.NameEn,
                OwnerAadhaarMasked: owner?.AadhaarMasked,
                OwnerMobileMasked: owner?.MobileMasked));
        }

        return results;
    }

    public async Task<Namuna10ReceiptContext?> GetReceiptByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        TaxReceipt? receipt = await dbContext.TaxReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        return receipt is null
            ? null
            : await HydrateReceiptContextAsync(receipt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> AllocateNextReceiptSequenceAsync(
        Guid tenantId,
        string financialYear,
        CancellationToken cancellationToken)
    {
        string fy = financialYear.Trim();
        DateTimeOffset now = dateTimeProvider.UtcNow;

        if (string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            const string sql = """
                INSERT INTO "receipt_counters" ("TenantId", "FinancialYear", "LastSequence", "UpdatedAtUtc")
                VALUES (@p0, @p1, 1, @p2)
                ON CONFLICT ("TenantId", "FinancialYear")
                DO UPDATE SET
                    "LastSequence" = "receipt_counters"."LastSequence" + 1,
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
                RETURNING "LastSequence" AS "Value";
                """;

            return await dbContext.Database
                .SqlQueryRaw<long>(
                    sql,
                    new NpgsqlParameter("p0", tenantId),
                    new NpgsqlParameter("p1", fy),
                    new NpgsqlParameter("p2", now))
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        ReceiptCounter? counter = await dbContext.ReceiptCounters
            .FirstOrDefaultAsync(c => c.FinancialYear == fy, cancellationToken)
            .ConfigureAwait(false);

        long nextSeq;
        if (counter is null)
        {
            nextSeq = 1;
            dbContext.ReceiptCounters.Add(new ReceiptCounter
            {
                TenantId = tenantId,
                FinancialYear = fy,
                LastSequence = 1,
                UpdatedAtUtc = now,
            });
        }
        else
        {
            nextSeq = counter.LastSequence + 1;
            counter.LastSequence = nextSeq;
            counter.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return nextSeq;
    }

    public Task AddReceiptAsync(TaxReceipt receipt, CancellationToken cancellationToken) =>
        dbContext.TaxReceipts.AddAsync(receipt, cancellationToken).AsTask();

    public Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken) =>
        dbContext.AuditLogs.AddAsync(log, cancellationToken).AsTask();

    public string FormatMarathiRupeesInWords(decimal amount) =>
        MarathiNumberToWords.ToRupeesWords(amount);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTx = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            await work(cancellationToken).ConfigureAwait(false);

            if (ownedTx is not null)
            {
                await ownedTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownedTx is not null)
            {
                await ownedTx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<Namuna10ReceiptContext?> HydrateReceiptContextAsync(
        TaxReceipt receipt,
        CancellationToken cancellationToken)
    {
        AssessmentYear? year = await dbContext.AssessmentYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == receipt.AssessmentYearId, cancellationToken)
            .ConfigureAwait(false);

        Property? prop = await dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == receipt.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        PropertyDemand? demand = await dbContext.PropertyDemands
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == receipt.PropertyDemandId, cancellationToken)
            .ConfigureAwait(false);

        if (year is null || prop is null || demand is null)
        {
            return null;
        }

        PropertyDemandBill? bill = receipt.DemandBillId.HasValue
            ? await dbContext.PropertyDemandBills
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == receipt.DemandBillId.Value, cancellationToken)
                .ConfigureAwait(false)
            : null;

        Dictionary<Guid, OwnerSummary> owners = await LoadPrimaryOwnersAsync([prop.Id], cancellationToken)
            .ConfigureAwait(false);
        owners.TryGetValue(prop.Id, out OwnerSummary? owner);

        return new Namuna10ReceiptContext(
            Receipt: receipt,
            Year: year,
            Property: prop,
            Demand: demand,
            Bill: bill,
            OwnerNameMr: owner?.NameMr,
            OwnerNameEn: owner?.NameEn,
            OwnerAadhaarMasked: owner?.AadhaarMasked,
            OwnerMobileMasked: owner?.MobileMasked);
    }

    private async Task<Dictionary<Guid, OwnerSummary>> LoadPrimaryOwnersAsync(
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken)
    {
        if (propertyIds.Count == 0)
        {
            return [];
        }

        var rows = await (
            from po in dbContext.PropertyOwners.AsNoTracking()
            join c in dbContext.Citizens.AsNoTracking() on po.CitizenId equals c.Id
            where po.IsActive && propertyIds.Contains(po.PropertyId)
            select new
            {
                po.PropertyId,
                po.IsPrimaryOwner,
                po.OwnershipSharePct,
                c.FullNameMr,
                c.FullNameEn,
                c.AadhaarLast4,
                c.MobileNo,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(o => o.PropertyId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var top = g.OrderByDescending(x => x.IsPrimaryOwner)
                        .ThenByDescending(x => x.OwnershipSharePct)
                        .First();

                    string? maskedAadhaar = !string.IsNullOrWhiteSpace(top.AadhaarLast4)
                        ? $"XXXX-XXXX-{top.AadhaarLast4}"
                        : null;
                    string? maskedMobile = !string.IsNullOrWhiteSpace(top.MobileNo) && top.MobileNo.Length >= 4
                        ? $"XXXXXX{top.MobileNo[^4..]}"
                        : null;

                    return new OwnerSummary(
                        top.FullNameMr,
                        top.FullNameEn,
                        maskedAadhaar,
                        maskedMobile,
                        top.MobileNo);
                });
    }

    private sealed record OwnerSummary(
        string? NameMr,
        string? NameEn,
        string? AadhaarMasked,
        string? MobileMasked,
        string? RawMobile);
}

