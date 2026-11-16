using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplySprint3MutationsRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                ALTER TABLE "property_mutations" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "property_mutations" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON "property_mutations";
                CREATE POLICY tenant_isolation ON "property_mutations"
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
                DROP POLICY IF EXISTS tenant_isolation ON "property_mutations";
                ALTER TABLE "property_mutations" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "property_mutations" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
