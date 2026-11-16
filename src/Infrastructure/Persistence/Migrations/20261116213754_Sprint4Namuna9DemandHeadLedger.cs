using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4Namuna9DemandHeadLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrearsByHeadJson",
                table: "property_demands",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "CollectedByHeadJson",
                table: "property_demands",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "CurrentByHeadJson",
                table: "property_demands",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "DemandRemarks",
                table: "property_demands",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EarlyRebateApplied",
                table: "property_demands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EarlyRebateEligible",
                table: "property_demands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "NoticeFee",
                table: "property_demands",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAtUtc",
                table: "property_demands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RebateCutoffDate",
                table: "property_demands",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarrantFee",
                table: "property_demands",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrearsByHeadJson",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "CollectedByHeadJson",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "CurrentByHeadJson",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "DemandRemarks",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "EarlyRebateApplied",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "EarlyRebateEligible",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "NoticeFee",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "RebateCutoffDate",
                table: "property_demands");

            migrationBuilder.DropColumn(
                name: "WarrantFee",
                table: "property_demands");
        }
    }
}
