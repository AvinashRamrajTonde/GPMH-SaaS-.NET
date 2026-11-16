using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplySprint5ReceiptsRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                ALTER TABLE "tax_receipts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "tax_receipts" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON "tax_receipts";
                CREATE POLICY tenant_isolation ON "tax_receipts"
                    USING ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.current_tenant_id', true), '')::uuid);

                ALTER TABLE "receipt_counters" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "receipt_counters" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON "receipt_counters";
                CREATE POLICY tenant_isolation ON "receipt_counters"
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
                DROP POLICY IF EXISTS tenant_isolation ON "tax_receipts";
                ALTER TABLE "tax_receipts" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "tax_receipts" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON "receipt_counters";
                ALTER TABLE "receipt_counters" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "receipt_counters" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
