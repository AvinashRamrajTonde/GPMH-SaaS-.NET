using System.Text;
using GPMH.Application;
using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure;
using GPMH.WebAPI.Middleware;
using GPMH.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    string? signingKey = builder.Configuration["Jwt:SigningKey"];
    if (!builder.Environment.IsDevelopment()
        && !builder.Environment.IsEnvironment("Testing")
        && string.IsNullOrWhiteSpace(signingKey))
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development/Testing environments.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            if (!string.IsNullOrWhiteSpace(signingKey))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "gpmh-api",
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "gpmh-clients",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.Zero,
                };
            }
        });

    builder.Services.AddAuthorization();

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

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseCors("FrontendPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TenantResolutionMiddleware>();

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

