using GPMH.Domain.Enums;

namespace GPMH.Application.DTOs.Auth;

public sealed record TenantMembershipSummaryDto(
    Guid TenantId,
    ApplicationRole Role,
    bool IsDefault);

public sealed record AuthenticatedUserDto(
    Guid UserId,
    string Username,
    string FullNameEn,
    string FullNameMr,
    ApplicationRole Role,
    Guid? ActiveTenantId,
    IReadOnlyList<TenantMembershipSummaryDto> Memberships);

public sealed record AuthTokenBundleDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserDto User);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserDto User,
    string? RefreshToken = null);

