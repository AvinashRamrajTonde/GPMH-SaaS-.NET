using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure.Auditing;
using GPMH.Infrastructure.Caching;
using GPMH.Infrastructure.Identity;
using GPMH.Infrastructure.Notifications;
using GPMH.Infrastructure.Persistence;
using GPMH.Infrastructure.Persistence.Repositories;
using GPMH.Infrastructure.Printing;
using GPMH.Infrastructure.Security;
using GPMH.Infrastructure.Services.LgdDirectory;
using GPMH.Infrastructure.Taxation;
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
        IConfiguration configuration,
        bool allowDevelopmentSqlite = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantRlsConnectionInterceptor>();
        services.AddScoped<TenantRlsConnectionOpenedInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<ITenantMembershipValidator, TenantMembershipValidator>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<ICitizenRepository, CitizenRepository>();
        services.AddScoped<IMutationRepository, MutationRepository>();
        services.AddScoped<IPartitionRepository, PartitionRepository>();
        services.AddScoped<INamuna9Repository, Namuna9Repository>();
        services.AddScoped<INamuna9KaRepository, Namuna9KaRepository>();
        services.AddScoped<INamuna10Repository, Namuna10Repository>();
        services.AddScoped<IFormulaMasterRepository, FormulaMasterRepository>();
        services.AddScoped<IDefaultFormulaSeeder, DefaultFormulaSeeder>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
        services.AddScoped<ISuperAdminProvisioningRepository, SuperAdminProvisioningRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.AddSingleton<IPiiProtectionService, AesGcmPiiProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthIdentityRepository, AuthIdentityRepository>();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        DevanagariFontRegistry.EnsureInitialized();
        services.AddSingleton<IExtractVerificationTokenService, ExtractVerificationTokenService>();
        services.AddSingleton<IQrCodeGeneratorService, QrCodeGeneratorService>();
        services.AddSingleton<IStatutoryPdfRenderer, QuestPdfStatutoryRenderer>();
        services.AddScoped<IPrintDataRepository, PrintDataRepository>();
        services.AddSingleton<IEmailService, LoggingEmailService>();
        services.AddSingleton<ISmsService, LoggingSmsService>();

        string? redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "gpmh:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        string databaseProvider = configuration["Database:Provider"] ?? "Postgres";
        bool useSqlite = string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);

        if (useSqlite && !allowDevelopmentSqlite)
        {
            throw new InvalidOperationException(
                "Database:Provider=Sqlite is permitted only in Development or Testing environments.");
        }

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (useSqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
            }

            options.AddInterceptors(
                sp.GetRequiredService<TenantRlsConnectionInterceptor>(),
                sp.GetRequiredService<TenantRlsConnectionOpenedInterceptor>(),
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
            .AddDbContextCheck<AppDbContext>(useSqlite ? "sqlite" : "postgresql");

        return services;
    }
}

