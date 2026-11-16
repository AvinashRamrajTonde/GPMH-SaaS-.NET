using System.Security.Cryptography;
using System.Text;
using GPMH.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace GPMH.Infrastructure.Security;

public sealed class AesGcmPiiProtectionService(IOptions<SecurityOptions> options)
    : IPiiProtectionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly SecurityOptions _options = options.Value;

    public byte[] EncryptBytes(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] key = Resolve32ByteKey(_options.PiiEncryptionKey, nameof(_options.PiiEncryptionKey));
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        using AesGcm aesGcm = new(key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);
        return payload;
    }

    public byte[] DecryptBytes(byte[] ciphertextPayload)
    {
        ArgumentNullException.ThrowIfNull(ciphertextPayload);
        if (ciphertextPayload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid encrypted PII payload length.");
        }

        byte[] key = Resolve32ByteKey(_options.PiiEncryptionKey, nameof(_options.PiiEncryptionKey));
        ReadOnlySpan<byte> payloadSpan = ciphertextPayload;
        ReadOnlySpan<byte> nonce = payloadSpan[..NonceSize];
        ReadOnlySpan<byte> tag = payloadSpan.Slice(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = payloadSpan[(NonceSize + TagSize)..];
        byte[] plaintext = new byte[ciphertext.Length];

        using AesGcm aesGcm = new(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public byte[] EncryptString(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return EncryptBytes(Encoding.UTF8.GetBytes(plaintext.Trim()));
    }

    public string DecryptString(byte[] ciphertextPayload) =>
        Encoding.UTF8.GetString(DecryptBytes(ciphertextPayload));

    public string ComputeBlindIndex(string canonicalValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalValue);
        byte[] key = Resolve32ByteKey(_options.BlindIndexKey, nameof(_options.BlindIndexKey));
        string normalized = canonicalValue.Trim().ToUpperInvariant();
        byte[] hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }

    public string MaskAadhaar(string rawAadhaar)
    {
        string digits = new([.. (rawAadhaar ?? string.Empty).Where(char.IsAsciiDigit)]);
        if (digits.Length < 4)
        {
            return "XXXX-XXXX-XXXX";
        }

        return $"XXXX-XXXX-{digits[^4..]}";
    }

    public string MaskPan(string rawPan)
    {
        string clean = (rawPan ?? string.Empty).Trim().ToUpperInvariant();
        if (clean.Length != 10)
        {
            return "**********";
        }

        return $"{clean[..2]}***{clean[5..]}";
    }

    private static byte[] Resolve32ByteKey(string rawKey, string settingName)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException($"Security:{settingName} is not configured.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(rawKey.Trim());
        }
        catch (FormatException)
        {
            decoded = Encoding.UTF8.GetBytes(rawKey.Trim());
        }

        if (decoded.Length == 32)
        {
            return decoded;
        }

        return SHA256.HashData(decoded);
    }
}

