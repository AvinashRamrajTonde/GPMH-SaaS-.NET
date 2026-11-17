using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Printing;
using GPMH.Infrastructure.Identity;
using GPMH.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace GPMH.Infrastructure.Printing;

public sealed class ExtractVerificationTokenService(
    IOptions<SecurityOptions> securityOptions,
    IOptions<JwtOptions> jwtOptions)
    : IExtractVerificationTokenService
{
    private const int PayloadLength = 56; // 16 + 16 + 16 + 8
    private const int ReceiptPayloadLength = 40; // 16 + 16 + 8
    private const int SignatureLength = 16; // 128-bit truncated HMAC-SHA256 tag
    private const int TotalTokenBytes = PayloadLength + SignatureLength;
    private const int TotalReceiptTokenBytes = ReceiptPayloadLength + SignatureLength;

    public string GenerateToken(ExtractVerifyPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] buffer = new byte[TotalTokenBytes];
        Span<byte> span = buffer;

        payload.TenantId.TryWriteBytes(span[..16]);
        payload.PropertyId.TryWriteBytes(span.Slice(16, 16));
        payload.AssessmentYearId.TryWriteBytes(span.Slice(32, 16));
        BinaryPrimitives.WriteInt64BigEndian(span.Slice(48, 8), payload.IssuedAtUtc.ToUnixTimeSeconds());

        byte[] fullMac = HMACSHA256.HashData(ResolveVerificationKey(), span[..PayloadLength]);
        fullMac.AsSpan(0, SignatureLength).CopyTo(span[PayloadLength..]);

        return Base64UrlEncode(buffer);
    }

    public bool TryValidateToken(string token, out ExtractVerifyPayloadDto? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!TryBase64UrlDecode(token.Trim(), out byte[]? bytes) || bytes is null || bytes.Length != TotalTokenBytes)
        {
            return false;
        }

        ReadOnlySpan<byte> body = bytes.AsSpan(0, PayloadLength);
        ReadOnlySpan<byte> providedMac = bytes.AsSpan(PayloadLength, SignatureLength);
        byte[] expectedMac = HMACSHA256.HashData(ResolveVerificationKey(), body);

        if (!CryptographicOperations.FixedTimeEquals(providedMac, expectedMac.AsSpan(0, SignatureLength)))
        {
            return false;
        }

        Guid tenantId = new(body[..16]);
        Guid propertyId = new(body.Slice(16, 16));
        Guid assessmentYearId = new(body.Slice(32, 16));
        long unixSeconds = BinaryPrimitives.ReadInt64BigEndian(body.Slice(48, 8));

        if (tenantId == Guid.Empty || propertyId == Guid.Empty)
        {
            return false;
        }

        DateTimeOffset issuedAtUtc;
        try
        {
            issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        payload = new ExtractVerifyPayloadDto(tenantId, propertyId, assessmentYearId, issuedAtUtc);
        return true;
    }

    public string GenerateReceiptToken(ReceiptVerifyPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] buffer = new byte[TotalReceiptTokenBytes];
        Span<byte> span = buffer;

        payload.TenantId.TryWriteBytes(span[..16]);
        payload.ReceiptId.TryWriteBytes(span.Slice(16, 16));
        BinaryPrimitives.WriteInt64BigEndian(span.Slice(32, 8), payload.IssuedAtUtc.ToUnixTimeSeconds());

        byte[] fullMac = HMACSHA256.HashData(ResolveReceiptVerificationKey(), span[..ReceiptPayloadLength]);
        fullMac.AsSpan(0, SignatureLength).CopyTo(span[ReceiptPayloadLength..]);

        return Base64UrlEncode(buffer);
    }

    public bool TryValidateReceiptToken(string token, out ReceiptVerifyPayloadDto? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!TryBase64UrlDecode(token.Trim(), out byte[]? bytes) || bytes is null || bytes.Length != TotalReceiptTokenBytes)
        {
            return false;
        }

        ReadOnlySpan<byte> body = bytes.AsSpan(0, ReceiptPayloadLength);
        ReadOnlySpan<byte> providedMac = bytes.AsSpan(ReceiptPayloadLength, SignatureLength);
        byte[] expectedMac = HMACSHA256.HashData(ResolveReceiptVerificationKey(), body);

        if (!CryptographicOperations.FixedTimeEquals(providedMac, expectedMac.AsSpan(0, SignatureLength)))
        {
            return false;
        }

        Guid tenantId = new(body[..16]);
        Guid receiptId = new(body.Slice(16, 16));
        long unixSeconds = BinaryPrimitives.ReadInt64BigEndian(body.Slice(32, 8));

        if (tenantId == Guid.Empty || receiptId == Guid.Empty)
        {
            return false;
        }

        DateTimeOffset issuedAtUtc;
        try
        {
            issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        payload = new ReceiptVerifyPayloadDto(tenantId, receiptId, issuedAtUtc);
        return true;
    }

    private byte[] ResolveVerificationKey() => DeriveDomainKey("GPMH:NAMUNA8:VERIFY:V1");

    private byte[] ResolveReceiptVerificationKey() => DeriveDomainKey("GPMH:NAMUNA10:RECEIPT:VERIFY:V1");

    private byte[] DeriveDomainKey(string domainPrefix)
    {
        string raw = !string.IsNullOrWhiteSpace(securityOptions.Value.BlindIndexKey)
            ? securityOptions.Value.BlindIndexKey
            : jwtOptions.Value.SigningKey;

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Verification HMAC signing key is not configured.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes($"{domainPrefix}:{raw.Trim()}"));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool TryBase64UrlDecode(string input, out byte[]? output)
    {
        output = null;
        string padded = input.Replace('-', '+').Replace('_', '/');
        int mod = padded.Length % 4;
        if (mod == 2)
        {
            padded += "==";
        }
        else if (mod == 3)
        {
            padded += "=";
        }
        else if (mod == 1)
        {
            return false;
        }

        try
        {
            output = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

