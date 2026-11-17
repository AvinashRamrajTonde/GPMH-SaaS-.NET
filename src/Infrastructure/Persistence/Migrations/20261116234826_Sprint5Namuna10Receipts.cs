using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5Namuna10Receipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_counters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_counters", x => new { x.TenantId, x.FinancialYear });
                });

            migrationBuilder.CreateTable(
                name: "tax_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyDemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookNo = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "BOOK-1"),
                    ReceiptNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReceiptSequence = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentMode = table.Column<int>(type: "integer", nullable: false),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ChequeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    TotalPaid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountGiven = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ExcessCredit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HeadAllocationJson = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CollectedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_receipts_assessment_years_AssessmentYearId",
                        column: x => x.AssessmentYearId,
                        principalTable: "assessment_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_receipts_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tax_receipts_property_demand_bills_DemandBillId",
                        column: x => x.DemandBillId,
                        principalTable: "property_demand_bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tax_receipts_property_demands_PropertyDemandId",
                        column: x => x.PropertyDemandId,
                        principalTable: "property_demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_AssessmentYearId",
                table: "tax_receipts",
                column: "AssessmentYearId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_DemandBillId",
                table: "tax_receipts",
                column: "DemandBillId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_PropertyDemandId",
                table: "tax_receipts",
                column: "PropertyDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_PropertyId",
                table: "tax_receipts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_TenantId_AssessmentYearId_ReceiptDate",
                table: "tax_receipts",
                columns: new[] { "TenantId", "AssessmentYearId", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_TenantId_OperationId",
                table: "tax_receipts",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_TenantId_PropertyId",
                table: "tax_receipts",
                columns: new[] { "TenantId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_TenantId_ReceiptNo",
                table: "tax_receipts",
                columns: new[] { "TenantId", "ReceiptNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_counters");

            migrationBuilder.DropTable(
                name: "tax_receipts");
        }
    }
}
