namespace GPMH.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Base64-encoded 32-byte AES-256-GCM symmetric encryption key for PII and private KYC docs.</summary>
    public string PiiEncryptionKey { get; set; } = string.Empty;

    /// <summary>Base64-encoded 32-byte HMAC-SHA256 key for deterministic blind index deduplication.</summary>
    public string BlindIndexKey { get; set; } = string.Empty;
}

