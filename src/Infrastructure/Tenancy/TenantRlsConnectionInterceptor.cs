using System.Data;
using System.Data.Common;
using GPMH.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GPMH.Infrastructure.Tenancy;

/// <summary>
/// Parameterized PostgreSQL transaction interceptor that binds app.current_tenant_id
/// cleanly to the active transaction scope (is_local = true) so PgBouncer pooled
/// connections never leak tenant RLS state across requests.
/// </summary>
public sealed class TenantRlsConnectionInterceptor(ITenantContext tenantContext)
    : DbTransactionInterceptor
{
    public const string RlsSettingKey = "app.current_tenant_id";

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);

        if (connection is NpgsqlConnection && tenantContext.CurrentTenantId is Guid tenantId)
        {
            using DbCommand cmd = BuildSetTenantCommand(connection, result, tenantId);
            cmd.ExecuteNonQuery();
        }

        return base.TransactionStarted(connection, eventData, result);
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);

        if (connection is NpgsqlConnection && tenantContext.CurrentTenantId is Guid tenantId)
        {
            await using DbCommand cmd = BuildSetTenantCommand(connection, result, tenantId);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public static DbCommand BuildSetTenantCommand(
        DbConnection connection,
        DbTransaction? transaction,
        Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty when setting RLS context.", nameof(tenantId));
        }

        DbCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT set_config(@settingKey, @tenantId, true);";
        cmd.CommandType = CommandType.Text;

        DbParameter keyParam = cmd.CreateParameter();
        keyParam.ParameterName = "@settingKey";
        keyParam.DbType = DbType.String;
        keyParam.Value = RlsSettingKey;
        cmd.Parameters.Add(keyParam);

        DbParameter tenantParam = cmd.CreateParameter();
        tenantParam.ParameterName = "@tenantId";
        tenantParam.DbType = DbType.String;
        tenantParam.Value = tenantId.ToString("D");
        cmd.Parameters.Add(tenantParam);

        return cmd;
    }
}
