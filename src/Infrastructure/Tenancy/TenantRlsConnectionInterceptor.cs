using System.Data;
using System.Data.Common;
using GPMH.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GPMH.Infrastructure.Tenancy;

/// <summary>
/// Parameterized PostgreSQL transaction interceptor that binds app.current_tenant_id
/// cleanly inside active transactions (is_local = true) so PgBouncer pooled
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

        if (connection is NpgsqlConnection)
        {
            string value = tenantContext.CurrentTenantId?.ToString("D") ?? string.Empty;
            using DbCommand cmd = BuildSetTenantCommand(connection, result, value, isLocal: true);
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

        if (connection is NpgsqlConnection)
        {
            string value = tenantContext.CurrentTenantId?.ToString("D") ?? string.Empty;
            await using DbCommand cmd = BuildSetTenantCommand(connection, result, value, isLocal: true);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public static DbCommand BuildSetTenantCommand(
        DbConnection connection,
        DbTransaction? transaction,
        Guid tenantId) =>
        BuildSetTenantCommand(connection, transaction, tenantId.ToString("D"), isLocal: true);

    public static DbCommand BuildSetTenantCommand(
        DbConnection connection,
        DbTransaction? transaction,
        string tenantValue,
        bool isLocal)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tenantValue);

        DbCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = isLocal
            ? "SELECT set_config(@settingKey, @tenantId, true);"
            : "SELECT set_config(@settingKey, @tenantId, false);";
        cmd.CommandType = CommandType.Text;

        DbParameter keyParam = cmd.CreateParameter();
        keyParam.ParameterName = "@settingKey";
        keyParam.DbType = DbType.String;
        keyParam.Value = RlsSettingKey;
        cmd.Parameters.Add(keyParam);

        DbParameter tenantParam = cmd.CreateParameter();
        tenantParam.ParameterName = "@tenantId";
        tenantParam.DbType = DbType.String;
        tenantParam.Value = tenantValue;
        cmd.Parameters.Add(tenantParam);

        return cmd;
    }
}

/// <summary>
/// Parameterized PostgreSQL connection-open interceptor setting or clearing
/// app.current_tenant_id on pooled connections even for read queries outside
/// explicit BeginTransaction scopes.
/// </summary>
public sealed class TenantRlsConnectionOpenedInterceptor(ITenantContext tenantContext)
    : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is NpgsqlConnection)
        {
            string value = tenantContext.CurrentTenantId?.ToString("D") ?? string.Empty;
            using DbCommand cmd = TenantRlsConnectionInterceptor.BuildSetTenantCommand(
                connection,
                transaction: null,
                value,
                isLocal: false);
            cmd.ExecuteNonQuery();
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is NpgsqlConnection)
        {
            string value = tenantContext.CurrentTenantId?.ToString("D") ?? string.Empty;
            await using DbCommand cmd = TenantRlsConnectionInterceptor.BuildSetTenantCommand(
                connection,
                transaction: null,
                value,
                isLocal: false);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
