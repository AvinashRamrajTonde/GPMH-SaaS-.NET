using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3PropertyMutations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "property_mutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MutationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreviousOwnersSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ProposedOwnersJson = table.Column<string>(type: "text", nullable: false),
                    DocumentReferenceNo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    SupportingAssetIdsJson = table.Column<string>(type: "text", nullable: true),
                    NoticeIssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NoticeEndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HasObjection = table.Column<bool>(type: "boolean", nullable: false),
                    ObjectionSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ObjectionRecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectionRecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResolutionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_mutations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_mutations_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_mutations_PropertyId",
                table: "property_mutations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_property_mutations_TenantId_ApplicationNo",
                table: "property_mutations",
                columns: new[] { "TenantId", "ApplicationNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_property_mutations_TenantId_PropertyId",
                table: "property_mutations",
                columns: new[] { "TenantId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_property_mutations_TenantId_Status",
                table: "property_mutations",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_mutations");
        }
    }
}
