using GPMH.Infrastructure.Services.LgdDirectory;
using GPMH.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GPMH.UnitTests;

public sealed class LgdUriAndRlsCommandTests
{
    [Theory]
    [InlineData("https://api.grampanchayatmh.in/api/v1", "https://api.grampanchayatmh.in/api/v1/")]
    [InlineData("https://api.grampanchayatmh.in/api/v1/", "https://api.grampanchayatmh.in/api/v1/")]
    public void NormalizeBaseUri_AlwaysPreservesTrailingSlash(string rawUrl, string expected)
    {
        Uri normalized = LgdDirectoryClient.NormalizeBaseUri(rawUrl);
        Assert.Equal(expected, normalized.ToString());
        Assert.Equal("https://api.grampanchayatmh.in/api/v1/panchayat/171391", new Uri(normalized, "panchayat/171391").ToString());
    }

    [Fact]
    public void BuildSetTenantCommand_UsesParameterizedSetConfigWithoutStringInterpolation()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        Guid tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        using System.Data.Common.DbCommand cmd = TenantRlsConnectionInterceptor.BuildSetTenantCommand(connection, transaction: null, tenantId);

        Assert.Equal("SELECT set_config(@settingKey, @tenantId, true);", cmd.CommandText);
        Assert.DoesNotContain(tenantId.ToString("D"), cmd.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TenantRlsConnectionInterceptor.RlsSettingKey, cmd.Parameters["@settingKey"].Value);
        Assert.Equal(tenantId.ToString("D"), cmd.Parameters["@tenantId"].Value);
    }
}

