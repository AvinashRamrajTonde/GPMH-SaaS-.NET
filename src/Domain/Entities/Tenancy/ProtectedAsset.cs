using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Tenancy;

/// <summary>
/// Encrypted private binary asset (digital signature, official stamp, UPI QR, or Agent KYC document)
/// stored outside public wwwroot and delivered only through authorized endpoints (ADR-015).
/// </summary>
public sealed class ProtectedAsset : BaseEntity, IGlobalEntity
{
    public required string AssetKey { get; set; }
    public Guid? TenantId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Category { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required byte[] EncryptedContent { get; set; }
    public long SizeBytes { get; set; }
}

