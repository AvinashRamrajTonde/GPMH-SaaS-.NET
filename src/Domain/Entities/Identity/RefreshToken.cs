using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Identity;

/// <summary>
/// Server-side SHA-256 hashed rotating refresh token with family-level reuse detection (ADR-006).
/// </summary>
public sealed class RefreshToken : BaseEntity, IGlobalEntity
{
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive(DateTimeOffset utcNow) => !RevokedAtUtc.HasValue && ExpiresAtUtc > utcNow;
}

