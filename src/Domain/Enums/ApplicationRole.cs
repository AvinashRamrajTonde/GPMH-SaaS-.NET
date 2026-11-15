namespace GPMH.Domain.Enums;

/// <summary>
/// Canonical 8 + 1 platform roles locked across backend RBAC and frontend authorization guards.
/// </summary>
public enum ApplicationRole
{
    SuperAdmin = 1,
    Gramsevak = 2,
    GP_Admin = 3,
    GP_Operator = 4,
    Surveyor = 5,
    Citizen = 6,
    Auditor = 7,
    Support_Admin = 8,
    Field_Agent = 9,
}

