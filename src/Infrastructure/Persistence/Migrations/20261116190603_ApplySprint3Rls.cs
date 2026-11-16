using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplySprint3Rls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            string[] tenantTables =
            [
                "property_owners",
                "property_constructions",
                "property_tax_calcs",
                "property_demands"
            ];

            foreach (var table in tenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                    CREATE POLICY tenant_isolation ON "{table}"
                        USING ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid)
                        WITH CHECK ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            string[] tenantTables =
            [
                "property_owners",
                "property_constructions",
                "property_tax_calcs",
                "property_demands"
            ];

            foreach (var table in tenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                    ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }
    }
}
