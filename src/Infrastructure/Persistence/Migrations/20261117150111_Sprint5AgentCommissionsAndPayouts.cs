using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5AgentCommissionsAndPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_commissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommissionType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RatePctSnapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SettledAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    LatestPayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_commissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_commissions_field_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "field_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VoucherSequence = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayoutAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UtrReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ReceiptAssetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_payouts_field_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "field_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_counters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_counters", x => new { x.TenantId, x.FinancialYear });
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_AgentId_Status_CreatedAtUtc",
                table: "agent_commissions",
                columns: new[] { "AgentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_SubscriptionPaymentId",
                table: "agent_commissions",
                column: "SubscriptionPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_TenantId_CreatedAtUtc",
                table: "agent_commissions",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_payouts_AgentId_Status_CreatedAtUtc",
                table: "agent_payouts",
                columns: new[] { "AgentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_payouts_UtrReference",
                table: "agent_payouts",
                column: "UtrReference");

            migrationBuilder.CreateIndex(
                name: "IX_agent_payouts_VoucherNo",
                table: "agent_payouts",
                column: "VoucherNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_commissions");

            migrationBuilder.DropTable(
                name: "agent_payouts");

            migrationBuilder.DropTable(
                name: "voucher_counters");
        }
    }
}
