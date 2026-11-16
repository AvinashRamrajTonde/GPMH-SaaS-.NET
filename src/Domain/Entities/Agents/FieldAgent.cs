using GPMH.Domain.Common;
using GPMH.Domain.Enums;

namespace GPMH.Domain.Entities.Agents;

/// <summary>
/// Field Sales Representative / District Franchise Partner with DPDP-protected Aadhaar and PAN KYC (ADR-012 / ADR-015).
/// </summary>
public sealed class FieldAgent : BaseEntity, IGlobalEntity
{
    public Guid UserId { get; set; }
    public required string AgentCode { get; set; }
    public AgentKycStatus KycStatus { get; set; } = AgentKycStatus.PendingApproval;
    public string? StatusReason { get; set; }
    public required string FullName { get; set; }
    public required string MobileNo { get; set; }
    public required string Email { get; set; }
    public string IdProofType { get; set; } = "AADHAAR";
    public required string PanMasked { get; set; }
    public required string AadhaarMasked { get; set; }
    public required string AadhaarBlindIndex { get; set; }
    public byte[]? AadhaarEncrypted { get; set; }
    public required string PanBlindIndex { get; set; }
    public byte[]? PanEncrypted { get; set; }
    public required string Address { get; set; }
    public required string VillageOrCity { get; set; }
    public required string District { get; set; }
    public required string TalukasCsv { get; set; }
    public string State { get; set; } = "Maharashtra";
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? UpiId { get; set; }
    public string? AadhaarDocumentKey { get; set; }
    public string? PanDocumentKey { get; set; }
    public decimal RequestedCommissionRatePct { get; set; } = 15m;
    public decimal ApprovedNewGpRatePct { get; set; } = 15m;
    public decimal ApprovedRenewalRatePct { get; set; } = 10m;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
}
