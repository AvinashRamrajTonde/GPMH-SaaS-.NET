namespace GPMH.Application.Common.Interfaces;

/// <summary>
/// DPDP Act 2023 cryptographic PII protection (AES-256-GCM AEAD encryption + HMAC-SHA256 blind indexing + masking).
/// </summary>
public interface IPiiProtectionService
{
    byte[] EncryptBytes(byte[] plaintext);

    byte[] DecryptBytes(byte[] ciphertextPayload);

    byte[] EncryptString(string plaintext);

    string DecryptString(byte[] ciphertextPayload);

    string ComputeBlindIndex(string canonicalValue);

    string MaskAadhaar(string rawAadhaar);

    string MaskPan(string rawPan);
}

