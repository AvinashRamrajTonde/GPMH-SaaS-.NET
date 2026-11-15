using GPMH.Domain.Common;

namespace GPMH.Domain.Entities.Identity;

public sealed class User : BaseEntity, IGlobalEntity
{
    public required string Username { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public required string FullNameMr { get; set; }
    public required string FullNameEn { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}

