using GPMH.Domain.Enums;

namespace GPMH.WebAPI.Authorization;

public static class AuthPolicies
{
    public const string RequireSuperAdmin = nameof(RequireSuperAdmin);
    public const string RequireSuperAdminOrSupportRead = nameof(RequireSuperAdminOrSupportRead);
    public const string RequireSuperAdminOrSupportExit = nameof(RequireSuperAdminOrSupportExit);
    public const string RequireGramsevak = nameof(RequireGramsevak);
    public const string RequireGpAdmin = nameof(RequireGpAdmin);
    public const string RequireGpStaff = nameof(RequireGpStaff);
    public const string RequireSurveyor = nameof(RequireSurveyor);
    public const string RequireFieldAgent = nameof(RequireFieldAgent);
    public const string RequireAuditorOrAdmin = nameof(RequireAuditorOrAdmin);
    public const string RequirePropertyRead = nameof(RequirePropertyRead);
    public const string RequirePropertyWrite = nameof(RequirePropertyWrite);

    public static IServiceCollection AddGpmhAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(RequireSuperAdmin, p => p.RequireRole(nameof(ApplicationRole.SuperAdmin)))
            .AddPolicy(RequireSuperAdminOrSupportRead, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Support_Admin)))
            .AddPolicy(RequireSuperAdminOrSupportExit, p => p.RequireAssertion(ctx =>
                ctx.User.IsInRole(nameof(ApplicationRole.SuperAdmin)) ||
                ctx.User.IsInRole(nameof(ApplicationRole.Support_Admin)) ||
                string.Equals(ctx.User.FindFirst("support_impersonation")?.Value, "true", StringComparison.OrdinalIgnoreCase)))
            .AddPolicy(RequireGramsevak, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak)))
            .AddPolicy(RequireGpAdmin, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin)))
            .AddPolicy(RequireGpStaff, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin),
                nameof(ApplicationRole.GP_Operator)))
            .AddPolicy(RequireSurveyor, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin),
                nameof(ApplicationRole.Surveyor)))
            .AddPolicy(RequireFieldAgent, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Support_Admin),
                nameof(ApplicationRole.Field_Agent)))
            .AddPolicy(RequireAuditorOrAdmin, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Auditor),
                nameof(ApplicationRole.Support_Admin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin)))
            .AddPolicy(RequirePropertyRead, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin),
                nameof(ApplicationRole.GP_Operator),
                nameof(ApplicationRole.Surveyor),
                nameof(ApplicationRole.Auditor)))
            .AddPolicy(RequirePropertyWrite, p => p.RequireRole(
                nameof(ApplicationRole.SuperAdmin),
                nameof(ApplicationRole.Gramsevak),
                nameof(ApplicationRole.GP_Admin),
                nameof(ApplicationRole.GP_Operator),
                nameof(ApplicationRole.Surveyor)));

        return services;
    }
}

