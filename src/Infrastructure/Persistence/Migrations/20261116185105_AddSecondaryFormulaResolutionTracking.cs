using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryFormulaResolutionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "water_tax_masters",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "water_tax_masters",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "water_tax_masters",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "usage_weightages",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "usage_weightages",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "usage_weightages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "depreciation_slabs",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "depreciation_slabs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "depreciation_slabs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastChangeReason",
                table: "area_slab_taxes",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastResolutionDate",
                table: "area_slab_taxes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResolutionNo",
                table: "area_slab_taxes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "water_tax_masters");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "water_tax_masters");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "water_tax_masters");

            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "usage_weightages");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "usage_weightages");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "usage_weightages");

            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "depreciation_slabs");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "depreciation_slabs");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "depreciation_slabs");

            migrationBuilder.DropColumn(
                name: "LastChangeReason",
                table: "area_slab_taxes");

            migrationBuilder.DropColumn(
                name: "LastResolutionDate",
                table: "area_slab_taxes");

            migrationBuilder.DropColumn(
                name: "LastResolutionNo",
                table: "area_slab_taxes");
        }
    }
}
