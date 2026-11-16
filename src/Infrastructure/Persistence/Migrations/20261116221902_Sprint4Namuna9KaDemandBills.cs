using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4Namuna9KaDemandBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bill_counters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_counters", x => new { x.TenantId, x.FinancialYear });
                });

            migrationBuilder.CreateTable(
                name: "property_demand_bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyDemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BillSequence = table.Column<long>(type: "bigint", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RebateCutoffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ArrearsAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NoticeAndPenaltyAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RebateAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetPayableAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HeadBreakdownJson = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    UpiIdUsed = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpiPayUri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_demand_bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_demand_bills_assessment_years_AssessmentYearId",
                        column: x => x.AssessmentYearId,
                        principalTable: "assessment_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_demand_bills_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_property_demand_bills_property_demands_PropertyDemandId",
                        column: x => x.PropertyDemandId,
                        principalTable: "property_demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_AssessmentYearId",
                table: "property_demand_bills",
                column: "AssessmentYearId");

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_PropertyDemandId",
                table: "property_demand_bills",
                column: "PropertyDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_PropertyId",
                table: "property_demand_bills",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_TenantId_BillNo",
                table: "property_demand_bills",
                columns: new[] { "TenantId", "BillNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_TenantId_PropertyDemandId",
                table: "property_demand_bills",
                columns: new[] { "TenantId", "PropertyDemandId" });

            migrationBuilder.CreateIndex(
                name: "IX_property_demand_bills_TenantId_PropertyId_AssessmentYearId_~",
                table: "property_demand_bills",
                columns: new[] { "TenantId", "PropertyId", "AssessmentYearId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_counters");

            migrationBuilder.DropTable(
                name: "property_demand_bills");
        }
    }
}
