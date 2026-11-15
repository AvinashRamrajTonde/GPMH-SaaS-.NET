using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Assessments;

/// <summary>
/// Tenant-scoped property owner / citizen master record with encrypted Aadhaar PII (ADR-015).
/// </summary>
public sealed class Citizen : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string CitizenCode { get; set; }
    public string? WardNo { get; set; }
    public required string FullNameMr { get; set; }
    public required string FullNameEn { get; set; }
    public required string Gender { get; set; }
    public string? MobileNo { get; set; }
    public byte[]? AadhaarEncrypted { get; set; }
    public string? AadhaarBlindIndex { get; set; }
    public string? AadhaarLast4 { get; set; }
    public string SocialCategory { get; set; } = "General";
    public string ExemptionCategory { get; set; } = "NotExempted";
    public bool IsActive { get; set; } = true;
}

