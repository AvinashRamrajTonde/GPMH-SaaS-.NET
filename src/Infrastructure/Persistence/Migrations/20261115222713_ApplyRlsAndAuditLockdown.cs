using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplyRlsAndAuditLockdown : Migration
    {
        private static readonly string[] TenantTables =
        [
            "gram_panchayats",
            "assessment_years",
            "citizens",
            "properties",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (string table in TenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                    CREATE POLICY tenant_isolation ON "{table}"
                    FOR ALL
                    USING (
                        nullif(current_setting('app.current_tenant_id', true), '') IS NOT NULL
                        AND "TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid
                    )
                    WITH CHECK (
                        nullif(current_setting('app.current_tenant_id', true), '') IS NOT NULL
                        AND "TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid
                    );
                    """);
            }

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gpmh_app') THEN
                        REVOKE UPDATE, DELETE ON TABLE audit_logs FROM gpmh_app;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (string table in TenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                    ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gpmh_app') THEN
                        GRANT UPDATE, DELETE ON TABLE audit_logs TO gpmh_app;
                    END IF;
                END $$;
                """);
        }
    }
}
