using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplySprint4Rls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                ALTER TABLE "property_demand_bills" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "property_demand_bills" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON "property_demand_bills";
                CREATE POLICY tenant_isolation ON "property_demand_bills"
                    USING ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid);

                ALTER TABLE "bill_counters" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "bill_counters" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON "bill_counters";
                CREATE POLICY tenant_isolation ON "bill_counters"
                    USING ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS tenant_isolation ON "property_demand_bills";
                ALTER TABLE "property_demand_bills" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "property_demand_bills" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON "bill_counters";
                ALTER TABLE "bill_counters" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "bill_counters" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
