using System.Globalization;
using System.Security.Cryptography;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Tenancy;
using GPMH.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Printing;

internal sealed record ResolvedUpiProfile(string? UpiId, string PayeeName);

internal sealed record ProtectedSignatoryAssets(
    byte[]? GramsevakSignatureBytes,
    byte[]? SarpanchSignatureBytes,
    byte[]? OperatorSignatureBytes,
    byte[]? OfficialStampBytes);

internal static class StatutoryPrintAssetHelper
{
    public static ResolvedUpiProfile ResolvePreferredUpi(
        IReadOnlyList<BankAccount> accounts,
        GramPanchayat? gp,
        string fallbackPayeeName)
    {
        BankAccount? preferred = accounts
            .Where(b => b.IsActive && !string.IsNullOrWhiteSpace(b.UpiId))
            .OrderByDescending(b => string.Equals(b.FundType, "GRAM_NIDHI", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(b => b.IsDefault)
            .FirstOrDefault();

        string? upiId = string.IsNullOrWhiteSpace(preferred?.UpiId) ? null : preferred.UpiId.Trim();
        if (string.IsNullOrWhiteSpace(upiId) && !string.IsNullOrWhiteSpace(gp?.UpiId))
        {
            upiId = gp.UpiId.Trim();
        }

        string? payeeCandidate = preferred?.UpiPayeeName;
        if (string.IsNullOrWhiteSpace(payeeCandidate))
        {
            payeeCandidate = gp?.UpiPayeeName;
        }
        if (string.IsNullOrWhiteSpace(payeeCandidate))
        {
            payeeCandidate = gp?.PanchayatNameEn;
        }
        if (string.IsNullOrWhiteSpace(payeeCandidate))
        {
            payeeCandidate = fallbackPayeeName;
        }

        string payeeName = string.IsNullOrWhiteSpace(payeeCandidate) ? "Gram Panchayat" : payeeCandidate.Trim();
        return new ResolvedUpiProfile(upiId, payeeName);
    }

    public static string? BuildUpiPayUri(
        string? upiId,
        string? payeeName,
        decimal amount,
        string transactionNote,
        string? transactionRef = null,
        bool twoDecimalPlaces = false)
    {
        if (string.IsNullOrWhiteSpace(upiId))
        {
            return null;
        }

        string safePa = Uri.EscapeDataString(upiId.Trim());
        string safePn = Uri.EscapeDataString(string.IsNullOrWhiteSpace(payeeName) ? "Gram Panchayat" : payeeName.Trim());
        string safeTn = Uri.EscapeDataString(transactionNote);

        string amPart = amount > 0m
            ? twoDecimalPlaces
                ? $"&am={amount.ToString("0.00", CultureInfo.InvariantCulture)}"
                : $"&am={Math.Round(amount, 0, MidpointRounding.AwayFromZero):0}"
            : string.Empty;

        string trPart = string.IsNullOrWhiteSpace(transactionRef)
            ? string.Empty
            : $"&tr={Uri.EscapeDataString(transactionRef.Trim())}";

        return $"upi://pay?pa={safePa}&pn={safePn}{amPart}{trPart}&tn={safeTn}&cu=INR";
    }

    public static async Task<byte[]?> TryLoadProtectedImageAsync(
        AppDbContext dbContext,
        IPiiProtectionService piiProtectionService,
        Guid tenantId,
        string? assetKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetKey))
        {
            return null;
        }

        ProtectedAsset? asset = await dbContext.ProtectedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssetKey == assetKey.Trim() && a.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return asset is null ? null : DecryptSafe(piiProtectionService, asset.EncryptedContent);
    }

    public static async Task<ProtectedSignatoryAssets> LoadSignatoryAssetsAsync(
        AppDbContext dbContext,
        IPiiProtectionService piiProtectionService,
        Guid tenantId,
        GramPanchayat? gp,
        CancellationToken cancellationToken)
    {
        if (gp is null)
        {
            return new ProtectedSignatoryAssets(null, null, null, null);
        }

        string? gsKey = NormalizeKey(gp.GramsevakSignatureKey);
        string? spKey = NormalizeKey(gp.SarpanchSignatureKey);
        string? opKey = NormalizeKey(gp.OperatorSignatureKey);
        string? stKey = NormalizeKey(gp.OfficialStampKey);

        List<string> keys = [.. new[] { gsKey, spKey, opKey, stKey }
            .Where(k => k is not null)
            .Select(k => k!)
            .Distinct(StringComparer.Ordinal)];

        if (keys.Count == 0)
        {
            return new ProtectedSignatoryAssets(null, null, null, null);
        }

        List<ProtectedAsset> assets = await dbContext.ProtectedAssets
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && keys.Contains(a.AssetKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, byte[]?> byKey = new(StringComparer.Ordinal);
        foreach (ProtectedAsset asset in assets)
        {
            byKey[asset.AssetKey] = DecryptSafe(piiProtectionService, asset.EncryptedContent);
        }

        return new ProtectedSignatoryAssets(
            GramsevakSignatureBytes: gsKey is not null && byKey.TryGetValue(gsKey, out byte[]? gs) ? gs : null,
            SarpanchSignatureBytes: spKey is not null && byKey.TryGetValue(spKey, out byte[]? sp) ? sp : null,
            OperatorSignatureBytes: opKey is not null && byKey.TryGetValue(opKey, out byte[]? op) ? op : null,
            OfficialStampBytes: stKey is not null && byKey.TryGetValue(stKey, out byte[]? st) ? st : null);
    }

    private static string? NormalizeKey(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    private static byte[]? DecryptSafe(IPiiProtectionService piiProtectionService, byte[] encrypted)
    {
        try
        {
            return piiProtectionService.DecryptBytes(encrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

