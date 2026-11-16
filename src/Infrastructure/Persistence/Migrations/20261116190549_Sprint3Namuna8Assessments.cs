using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3Namuna8Assessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitySurveyOrGutNo",
                table: "properties",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccupierName",
                table: "properties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherTax",
                table: "properties",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PropertyDescription",
                table: "properties",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "properties",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StreetOrLaneName",
                table: "properties",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_constructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorNo = table.Column<int>(type: "integer", nullable: false),
                    FloorLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConstructionTypeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UsageCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LengthFt = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    WidthFt = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    AreaSqFt = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    AreaSqM = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    ConstructionStartYear = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_constructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_constructions_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_demands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrearsBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CurrentDemand = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RebateAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PendingBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_demands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_demands_assessment_years_AssessmentYearId",
                        column: x => x.AssessmentYearId,
                        principalTable: "assessment_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_demands_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_owners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipSharePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsPrimaryOwner = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_owners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_owners_citizens_CitizenId",
                        column: x => x.CitizenId,
                        principalTable: "citizens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_owners_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_tax_calcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    OpenLandTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LightingTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HealthTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SanitationTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ToiletTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    WaterTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    OtherTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_tax_calcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_tax_calcs_assessment_years_AssessmentYearId",
                        column: x => x.AssessmentYearId,
                        principalTable: "assessment_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_tax_calcs_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_constructions_PropertyId",
                table: "property_constructions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_constructions_TenantId_PropertyId_FloorNo",
                table: "property_constructions",
                columns: new[] { "TenantId", "PropertyId", "FloorNo" });

            migrationBuilder.CreateIndex(
                name: "IX_property_demands_AssessmentYearId",
                table: "property_demands",
                column: "AssessmentYearId");

            migrationBuilder.CreateIndex(
                name: "IX_property_demands_PropertyId",
                table: "property_demands",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_demands_TenantId_PropertyId_AssessmentYearId",
                table: "property_demands",
                columns: new[] { "TenantId", "PropertyId", "AssessmentYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_property_owners_CitizenId",
                table: "property_owners",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_property_owners_PropertyId",
                table: "property_owners",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_owners_TenantId_PropertyId_CitizenId",
                table: "property_owners",
                columns: new[] { "TenantId", "PropertyId", "CitizenId" });

            migrationBuilder.CreateIndex(
                name: "IX_property_tax_calcs_AssessmentYearId",
                table: "property_tax_calcs",
                column: "AssessmentYearId");

            migrationBuilder.CreateIndex(
                name: "IX_property_tax_calcs_PropertyId",
                table: "property_tax_calcs",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_tax_calcs_TenantId_PropertyId_AssessmentYearId",
                table: "property_tax_calcs",
                columns: new[] { "TenantId", "PropertyId", "AssessmentYearId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_constructions");

            migrationBuilder.DropTable(
                name: "property_demands");

            migrationBuilder.DropTable(
                name: "property_owners");

            migrationBuilder.DropTable(
                name: "property_tax_calcs");

            migrationBuilder.DropColumn(
                name: "CitySurveyOrGutNo",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "OccupierName",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "OtherTax",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "PropertyDescription",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "StreetOrLaneName",
                table: "properties");
        }
    }
}
