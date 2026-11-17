using System.Security.Cryptography;
using System.Text;
using GPMH.Application;
using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure;
using GPMH.Infrastructure.Identity;
using GPMH.Infrastructure.Security;
using GPMH.WebAPI.Authorization;
using GPMH.WebAPI.Logging;
using GPMH.WebAPI.Middleware;
using GPMH.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With(new SerilogTelemetryEnricher(context.HostingEnvironment.EnvironmentName))
            .WriteTo.Console();

        if (!context.HostingEnvironment.IsEnvironment("Testing"))
        {
            configuration.WriteTo.File(
                path: "logs/gpmh-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true);
        }
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

    bool allowDevelopmentSqlite = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, allowDevelopmentSqlite);

    string? ephemeralKey = (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        : null;

    builder.Services.PostConfigure<JwtOptions>(opts =>
    {
        if (string.IsNullOrWhiteSpace(opts.SigningKey) && ephemeralKey is not null)
        {
            opts.SigningKey = ephemeralKey;
        }
    });

    string? ephemeralPiiKey = (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        : null;
    string? ephemeralBlindKey = (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        : null;

    builder.Services.PostConfigure<SecurityOptions>(opts =>
    {
        if (string.IsNullOrWhiteSpace(opts.PiiEncryptionKey) && ephemeralPiiKey is not null)
        {
            opts.PiiEncryptionKey = ephemeralPiiKey;
        }

        if (string.IsNullOrWhiteSpace(opts.BlindIndexKey) && ephemeralBlindKey is not null)
        {
            opts.BlindIndexKey = ephemeralBlindKey;
        }
    });

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((jwtBearer, jwtOpts) =>
        {
            JwtOptions resolved = jwtOpts.Value;
            if (string.IsNullOrWhiteSpace(resolved.SigningKey))
            {
                throw new InvalidOperationException("Jwt:SigningKey must be configured.");
            }

            jwtBearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = resolved.Issuer,
                ValidAudience = resolved.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(resolved.SigningKey)),
                ClockSkew = TimeSpan.Zero,
            };
        });

    builder.Services.AddGpmhAuthorization();

    string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendPolicy", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    WebApplication app = builder.Build();

    if (!app.Environment.IsDevelopment()
        && !app.Environment.IsEnvironment("Testing")
        && (string.IsNullOrWhiteSpace(app.Configuration["Jwt:SigningKey"]) ||
            string.IsNullOrWhiteSpace(app.Configuration["Security:PiiEncryptionKey"]) ||
            string.IsNullOrWhiteSpace(app.Configuration["Security:BlindIndexKey"])))
    {
        throw new InvalidOperationException("Jwt:SigningKey and Security PII keys must be configured outside Development/Testing environments.");
    }

    if (!app.Environment.IsDevelopment()
        && !app.Environment.IsEnvironment("Testing")
        && string.Equals(app.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Database:Provider=Sqlite is strictly disallowed outside Development/Testing environments.");
    }

    if (app.Environment.IsDevelopment()
        && string.Equals(app.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        await app.Services.InitializeDevelopmentDatabaseAsync(app.Configuration, CancellationToken.None);
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.MapGet("/swagger", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
    }

    app.UseCors("FrontendPolicy");
    app.UseAuthentication();
    app.UseMiddleware<ImpersonationWriteGuardMiddleware>();
    app.UseAuthorization();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseMiddleware<TenantLicenseMiddleware>();

    app.MapControllers();
    app.MapHealthChecks("/health/ready");

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

namespace GPMH.WebAPI
{
    public partial class Program
    {
    }
}

