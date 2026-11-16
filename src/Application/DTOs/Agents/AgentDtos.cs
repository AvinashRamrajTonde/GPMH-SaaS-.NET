using GPMH.Domain.Enums;

namespace GPMH.Application.DTOs.Agents;

public sealed record AgentRegistrationResultDto(
    Guid AgentId,
    Guid UserId,
    string AgentCode,
    string FullName,
    string MobileNo,
    AgentKycStatus KycStatus,
    string AadhaarMasked,
    string PanMasked,
    decimal RequestedCommissionRatePct);

public sealed record AgentProfileDto(
    Guid AgentId,
    Guid UserId,
    string AgentCode,
    AgentKycStatus KycStatus,
    string? StatusReason,
    string FullName,
    string MobileNo,
    string Email,
    string District,
    string TalukasCsv,
    string AadhaarMasked,
    string PanMasked,
    decimal RequestedCommissionRatePct,
    decimal ApprovedNewGpRatePct,
    decimal ApprovedRenewalRatePct,
    string? AadhaarDocumentKey,
    string? PanDocumentKey);

