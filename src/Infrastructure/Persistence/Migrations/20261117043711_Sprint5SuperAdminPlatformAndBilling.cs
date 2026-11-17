using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5SuperAdminPlatformAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSupportAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCodeUsed",
                table: "tenants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByAgentId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "tenant_subscription_payments",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNo",
                table: "tenant_subscription_payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "tenant_subscription_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "audit_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateTable(
                name: "platform_payment_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IfscCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpiId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AnnualFee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    GstPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Gstin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SupportPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SupportEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_payment_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_payment_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    UtrReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlipAssetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedSubscriptionPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_payment_submissions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "platform_payment_accounts",
                columns: new[] { "Id", "AccountName", "AccountNumber", "AnnualFee", "BankName", "BranchName", "CreatedAtUtc", "GstPercent", "Gstin", "IfscCode", "IsActive", "SupportEmail", "SupportPhone", "UpdatedAtUtc", "UpiId" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111116000"), "Maharashtra GP Digital Network", "39876543210", 6000.00m, "State Bank of India", "Nariman Point, Mumbai", new DateTimeOffset(new DateTime(2026, 4, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 18.00m, "27AABCM1234F1Z5", "SBIN0001234", true, "support@epanchayat-mh.gov.in", "0721-2550101", null, "epanchayatraj@sbi" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsDeleted_Status",
                table: "tenants",
                columns: new[] { "IsDeleted", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscription_payments_InvoiceNo",
                table: "tenant_subscription_payments",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_submissions_InvoiceNo",
                table: "tenant_payment_submissions",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_submissions_Status",
                table: "tenant_payment_submissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_submissions_TenantId_Status",
                table: "tenant_payment_submissions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_submissions_UtrReference",
                table: "tenant_payment_submissions",
                column: "UtrReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_payment_accounts");

            migrationBuilder.DropTable(
                name: "tenant_payment_submissions");

            migrationBuilder.DropIndex(
                name: "IX_tenants_IsDeleted_Status",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenant_subscription_payments_InvoiceNo",
                table: "tenant_subscription_payments");

            migrationBuilder.DropColumn(
                name: "IsSupportAdmin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ReferralCodeUsed",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ReferredByAgentId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "tenant_subscription_payments");

            migrationBuilder.DropColumn(
                name: "InvoiceNo",
                table: "tenant_subscription_payments");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "tenant_subscription_payments");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "audit_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
