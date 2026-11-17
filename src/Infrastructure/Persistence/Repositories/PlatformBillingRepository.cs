using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Dashboard;
using GPMH.Application.DTOs.SuperAdmin;
using GPMH.Domain.Entities.Auditing;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Domain.Enums;
using GPMH.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

/// <summary>
/// Manages singleton platform payment settings, GP subscription status and UTR slip submissions,
/// invoice sequencing, and middleware license lookups without leaking Persistence into WebAPI.
/// </summary>
public sealed class PlatformBillingRepository(
    AppDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IPlatformBillingRepository, ITenantLicenseChecker
{
    public async Task<TenantLicenseState?> GetLicenseStateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.IsDeleted,
                t.Status,
                t.SubscriptionExpiresAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new TenantLicenseState(
                Exists: true,
                IsDeleted: row.IsDeleted,
                Status: row.Status,
                SubscriptionExpiresAtUtc: row.SubscriptionExpiresAtUtc);
    }

    public async Task<PlatformPaymentAccount> GetOrCreatePlatformAccountAsync(CancellationToken cancellationToken)
    {
        PlatformPaymentAccount? account = await dbContext.PlatformPaymentAccounts
            .FirstOrDefaultAsync(a => a.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (account is not null)
        {
            return account;
        }

        account = new PlatformPaymentAccount
        {
            Id = PlatformPaymentAccount.DefaultAccountId,
            AccountName = "Maharashtra GP Digital Network",
            BankName = "State Bank of India",
            BranchName = "Nariman Point, Mumbai",
            AccountNumber = "39876543210",
            IfscCode = "SBIN0001234",
            UpiId = "epanchayatraj@sbi",
            AnnualFee = 6000.00m,
            GstPercent = 18.00m,
            Gstin = "27AABCM1234F1Z5",
            SupportPhone = "0721-2550101",
            SupportEmail = "support@epanchayat-mh.gov.in",
            IsActive = true,
        };

        await dbContext.PlatformPaymentAccounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return account;
    }

    public async Task<PlatformPaymentAccount> UpdatePlatformAccountAsync(
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
        CancellationToken cancellationToken)
    {
        PlatformPaymentAccount account = await GetOrCreatePlatformAccountAsync(cancellationToken).ConfigureAwait(false);

        account.AccountName = accountName.Trim();
        account.BankName = bankName.Trim();
        account.BranchName = branchName.Trim();
        account.AccountNumber = accountNumber.Trim();
        account.IfscCode = ifscCode.Trim().ToUpperInvariant();
        account.UpiId = upiId.Trim();
        account.AnnualFee = Math.Round(annualFee, 2, MidpointRounding.AwayFromZero);
        account.GstPercent = Math.Round(gstPercent, 2, MidpointRounding.AwayFromZero);
        account.Gstin = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();
        account.SupportPhone = string.IsNullOrWhiteSpace(supportPhone) ? null : supportPhone.Trim();
        account.SupportEmail = string.IsNullOrWhiteSpace(supportEmail) ? null : supportEmail.Trim();
        account.UpdatedAtUtc = dateTimeProvider.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return account;
    }

    public async Task<GpSubscriptionStatusDto> GetGpSubscriptionStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        Tenant tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        GramPanchayat? gp = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        PlatformPaymentAccount plat = await GetOrCreatePlatformAccountAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = dateTimeProvider.UtcNow;
        int? daysUntilExpiry = tenant.GetDaysUntilExpiry(now);
        bool showWarning = daysUntilExpiry.HasValue && daysUntilExpiry.Value <= 30;

        List<TenantPaymentSubmission> submissions = await dbContext.TenantPaymentSubmissions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> userIds = [.. submissions.Select(s => s.SubmittedByUserId).Distinct()];
        Dictionary<Guid, string> usernames = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken)
            .ConfigureAwait(false);

        List<TenantPaymentSubmissionDto> submissionDtos = submissions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new TenantPaymentSubmissionDto(
                SubmissionId: s.Id,
                TenantId: s.TenantId,
                LgdCode: tenant.LgdCode,
                PanchayatNameEn: tenant.NameEn,
                PanchayatNameMr: tenant.NameMr,
                DistrictName: gp?.DistrictName ?? string.Empty,
                SubmittedByUserId: s.SubmittedByUserId,
                SubmittedByUsername: usernames.GetValueOrDefault(s.SubmittedByUserId, "-"),
                PlanName: s.PlanName,
                PaymentMode: s.PaymentMode,
                Amount: s.Amount,
                UtrReference: s.UtrReference,
                PaymentDate: s.PaymentDate,
                SlipAssetKey: s.SlipAssetKey,
                Status: s.Status,
                RejectionReason: s.RejectionReason,
                ReviewedByUserId: s.ReviewedByUserId,
                ReviewedAtUtc: s.ReviewedAtUtc,
                ApprovedSubscriptionPaymentId: s.ApprovedSubscriptionPaymentId,
                InvoiceNo: s.InvoiceNo,
                Notes: s.Notes,
                CreatedAtUtc: s.CreatedAtUtc))
            .ToList();

        List<TenantSubscriptionPayment> payments = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TenantSubscriptionHistoryItemDto> historyDtos = payments
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new TenantSubscriptionHistoryItemDto(
                PaymentId: p.Id,
                PlanName: p.PlanName,
                PaymentMode: p.PaymentMode,
                Amount: p.Amount,
                GstAmount: p.GstAmount,
                DurationDays: p.DurationDays,
                PaymentReference: p.PaymentReference,
                InvoiceNo: p.InvoiceNo,
                PaymentDateUtc: p.PaymentDateUtc,
                Notes: p.Notes))
            .ToList();

        return new GpSubscriptionStatusDto(
            TenantId: tenant.Id,
            LgdCode: tenant.LgdCode,
            PanchayatNameEn: tenant.NameEn,
            PanchayatNameMr: tenant.NameMr,
            Status: tenant.Status.ToString(),
            EffectiveStatus: tenant.GetEffectiveStatus(now),
            SubscriptionExpiresAtUtc: tenant.SubscriptionExpiresAtUtc,
            DaysUntilExpiry: daysUntilExpiry,
            ShowExpiryWarning30Days: showWarning,
            PlatformPaymentAccount: new PlatformPaymentAccountSummaryDto(
                AccountName: plat.AccountName,
                BankName: plat.BankName,
                BranchName: plat.BranchName,
                AccountNumber: plat.AccountNumber,
                IfscCode: plat.IfscCode,
                UpiId: plat.UpiId,
                AnnualFee: plat.AnnualFee,
                GstPercent: plat.GstPercent,
                Gstin: plat.Gstin,
                SupportPhone: plat.SupportPhone,
                SupportEmail: plat.SupportEmail),
            RecentSubmissions: submissionDtos,
            PaymentHistory: historyDtos);
    }

    public async Task<bool> ExistsNonRejectedUtrAsync(
        string utrReference,
        Guid? excludeSubmissionId,
        CancellationToken cancellationToken)
    {
        string normalized = utrReference.Trim().ToUpperInvariant();

        List<string> subUtrs = await dbContext.TenantPaymentSubmissions
            .AsNoTracking()
            .Where(s => s.Status != TenantPaymentSubmission.StatusRejected &&
                        (!excludeSubmissionId.HasValue || s.Id != excludeSubmissionId.Value))
            .Select(s => s.UtrReference)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subUtrs.Any(utr => string.Equals(utr.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        List<string?> directRefs = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .Where(p => p.PaymentReference != null)
            .Select(p => p.PaymentReference)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return directRefs.Any(r => !string.IsNullOrWhiteSpace(r) &&
            string.Equals(r.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> ExistsSlipAssetInTenantAsync(Guid tenantId, string assetKey, CancellationToken cancellationToken) =>
        dbContext.ProtectedAssets
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.AssetKey == assetKey, cancellationToken);

    public async Task AddPaymentSubmissionAsync(
        TenantPaymentSubmission submission,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        await dbContext.TenantPaymentSubmissions.AddAsync(submission, cancellationToken).ConfigureAwait(false);
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GenerateNextInvoiceNumberAsync(int year, CancellationToken cancellationToken)
    {
        string prefix = $"INV-{year}-";

        List<string?> existingInvoices = await dbContext.TenantSubscriptionPayments
            .AsNoTracking()
            .Where(p => p.InvoiceNo != null && p.InvoiceNo.StartsWith(prefix))
            .Select(p => p.InvoiceNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int maxSeq = 0;
        foreach (string? inv in existingInvoices)
        {
            if (!string.IsNullOrWhiteSpace(inv) &&
                int.TryParse(inv[prefix.Length..], out int parsed) &&
                parsed > maxSeq)
            {
                maxSeq = parsed;
            }
        }

        return $"{prefix}{maxSeq + 1:D4}";
    }

    public async Task<GstSubscriptionInvoicePdfModel?> BuildGstInvoiceModelAsync(
        Guid submissionOrPaymentId,
        Guid? restrictToTenantId,
        CancellationToken cancellationToken)
    {
        TenantPaymentSubmission? sub = await dbContext.TenantPaymentSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == submissionOrPaymentId || s.ApprovedSubscriptionPaymentId == submissionOrPaymentId,
                cancellationToken)
            .ConfigureAwait(false);

        TenantSubscriptionPayment? payment = sub?.ApprovedSubscriptionPaymentId is Guid payId
            ? await dbContext.TenantSubscriptionPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payId, cancellationToken)
                .ConfigureAwait(false)
            : await dbContext.TenantSubscriptionPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == submissionOrPaymentId, cancellationToken)
                .ConfigureAwait(false);

        if (payment is null)
        {
            return null;
        }

        if (restrictToTenantId.HasValue && payment.TenantId != restrictToTenantId.Value)
        {
            return null;
        }

        Tenant? tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == payment.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return null;
        }

        GramPanchayat? gp = await dbContext.GramPanchayats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenant.Id, cancellationToken)
            .ConfigureAwait(false);

        PlatformPaymentAccount plat = await GetOrCreatePlatformAccountAsync(cancellationToken).ConfigureAwait(false);

        decimal total = payment.Amount;
        decimal gstPct = plat.GstPercent > 0m ? plat.GstPercent : 18.00m;
        decimal gstAmt = payment.GstAmount > 0m
            ? payment.GstAmount
            : Math.Round(total - (total / (1m + (gstPct / 100m))), 2, MidpointRounding.AwayFromZero);
        decimal taxableBase = Math.Round(total - gstAmt, 2, MidpointRounding.AwayFromZero);

        string invoiceNo = payment.InvoiceNo
            ?? sub?.InvoiceNo
            ?? $"INV-{payment.PaymentDateUtc.Year}-{payment.Id.ToString("N")[..6].ToUpperInvariant()}";

        return new GstSubscriptionInvoicePdfModel(
            InvoiceNo: invoiceNo,
            IssuedAtUtc: payment.PaymentDateUtc,
            TenantId: tenant.Id,
            LgdCode: tenant.LgdCode,
            PanchayatNameEn: tenant.NameEn,
            PanchayatNameMr: tenant.NameMr,
            DistrictName: gp?.DistrictName ?? "-",
            TehsilName: gp?.TehsilName ?? "-",
            OfficeAddress: gp?.OfficeAddress,
            Pincode: gp?.Pincode,
            PlanName: payment.PlanName,
            PaymentMode: payment.PaymentMode,
            UtrReference: payment.PaymentReference ?? sub?.UtrReference ?? "-",
            PaymentDate: sub?.PaymentDate ?? DateOnly.FromDateTime(payment.PaymentDateUtc.UtcDateTime),
            DurationDays: payment.DurationDays,
            ValidUntilUtc: tenant.SubscriptionExpiresAtUtc,
            TotalAmountPaid: total,
            TaxableBaseAmount: taxableBase,
            GstPercent: gstPct,
            GstAmount: gstAmt,
            AmountInWordsMr: MarathiNumberToWords.ToRupeesWords(total),
            VendorAccountName: plat.AccountName,
            VendorBankName: plat.BankName,
            VendorAccountNumber: plat.AccountNumber,
            VendorIfscCode: plat.IfscCode,
            VendorUpiId: plat.UpiId,
            VendorGstin: plat.Gstin,
            SupportPhone: plat.SupportPhone,
            SupportEmail: plat.SupportEmail);
    }
}

