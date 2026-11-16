using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormulaResolutionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "formula_settings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "formula_settings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "formula_settings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "construction_type_masters",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "construction_type_masters",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "construction_type_masters",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "formula_settings");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "formula_settings");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "formula_settings");

            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "construction_type_masters");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "construction_type_masters");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "construction_type_masters");
        }
    }
}
