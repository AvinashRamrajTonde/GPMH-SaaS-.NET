using System.Security.Claims;
using System.Text.Encodings.Web;
using GPMH.Application.Common.Interfaces;
using GPMH.Infrastructure.Auditing;
using GPMH.Infrastructure.Persistence;
using GPMH.Infrastructure.Services.LgdDirectory;
using GPMH.Infrastructure.Tenancy;
using GPMH.WebAPI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GPMH.IntegrationTests;

public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    public List<Uri?> RecordedUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RecordedUris.Add(request.RequestUri);

        string json = """
        {
          "success": true,
          "execution_time_ms": 1.2,
          "data": {
            "lgd_code": "171391",
            "gp_name_en": "Chincholi Khurd",
            "gp_name_mr": "चिंचोली खुर्द",
            "district": "Beed",
            "district_code": "497",
            "tehsil": "Dharur",
            "tehsil_code": "4192",
            "pincode": "431124"
          }
        }
        """;

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestBearer";
    public const string TestUserIdHeader = "X-Test-User-Id";
    public const string TestUserRoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestUserIdHeader, out Microsoft.Extensions.Primitives.StringValues userIdHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string role = Request.Headers.TryGetValue(TestUserRoleHeader, out Microsoft.Extensions.Primitives.StringValues roleHeader)
            ? roleHeader.ToString()
            : "Gramsevak";

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userIdHeader.ToString()),
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class GpmhApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection _sqliteConnection = null!;
    public RecordingHttpMessageHandler LgdHandler { get; } = new();

    public async Task InitializeAsync()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        await _sqliteConnection.OpenAsync();

        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqliteConnection.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite(_sqliteConnection);
                options.AddInterceptors(
                    sp.GetRequiredService<TenantRlsConnectionInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Replace LGD client HTTP transport intentionally without trailing '/' in config URL
            // to prove LgdDirectoryClient.NormalizeBaseUri prevents dropping "/api/v1/".
            services.AddHttpClient<ILgdDirectoryClient, LgdDirectoryClient>(client =>
            {
                client.BaseAddress = LgdDirectoryClient.NormalizeBaseUri("https://api.grampanchayatmh.in/api/v1");
            }).ConfigurePrimaryHttpMessageHandler(() => LgdHandler);
        });
    }
}
