using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure.Auditing;
using GPMH.Infrastructure.Persistence;
using GPMH.Infrastructure.Services.LgdDirectory;
using GPMH.Infrastructure.Tenancy;
using GPMH.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GPMH.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsConnectionInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<ITenantMembershipValidator, TenantMembershipValidator>();

        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });

            options.AddInterceptors(
                sp.GetRequiredService<TenantRlsConnectionInterceptor>(),
                sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        LgdDirectoryOptions lgdOptions = new();
        configuration.GetSection(LgdDirectoryOptions.SectionName).Bind(lgdOptions);

        services.AddHttpClient<ILgdDirectoryClient, LgdDirectoryClient>(client =>
        {
            client.BaseAddress = LgdDirectoryClient.NormalizeBaseUri(lgdOptions.LgdApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddStandardResilienceHandler();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgresql");

        return services;
    }
}

