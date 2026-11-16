using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplySprint2Rls : Migration
    {
        private static readonly string[] Sprint2TenantTables =
        [
            "bank_accounts",
            "formula_settings",
            "construction_type_masters",
            "usage_weightages",
            "depreciation_slabs",
            "area_slab_taxes",
            "water_tax_masters",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (string table in Sprint2TenantTables)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (string table in Sprint2TenantTables)
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
