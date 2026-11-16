using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPMH.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2OnboardingDashboardAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CensusCode",
                table: "gram_panchayats",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GramsevakMobile",
                table: "gram_panchayats",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GramsevakName",
                table: "gram_panchayats",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GramsevakSignatureKey",
                table: "gram_panchayats",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArrearsRolloverInitialized",
                table: "gram_panchayats",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OfficeAddress",
                table: "gram_panchayats",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialStampKey",
                table: "gram_panchayats",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorMobile",
                table: "gram_panchayats",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorName",
                table: "gram_panchayats",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorSignatureKey",
                table: "gram_panchayats",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvenanceJson",
                table: "gram_panchayats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SarpanchMobile",
                table: "gram_panchayats",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SarpanchName",
                table: "gram_panchayats",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SarpanchSignatureKey",
                table: "gram_panchayats",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetupProgressPct",
                table: "gram_panchayats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpSarpanchName",
                table: "gram_panchayats",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "area_slab_taxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaFrom = table.Column<int>(type: "integer", nullable: false),
                    AreaTo = table.Column<int>(type: "integer", nullable: false),
                    LightingTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HealthTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SanitationTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ToiletTax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_slab_taxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FundType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IfscCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpiId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpiPayeeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QrImageKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "construction_type_masters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TypeName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LandAnnualValueRate = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ConstructionReadyReckonerRate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    UsageWeightageFactor = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_type_masters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "depreciation_slabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgeFrom = table.Column<int>(type: "integer", nullable: false),
                    AgeTo = table.Column<int>(type: "integer", nullable: false),
                    FromInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    ToInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    HutMudPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    StoneBrickRccPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depreciation_slabs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "field_agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KycStatus = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MobileNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdProofType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PanMasked = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AadhaarMasked = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AadhaarBlindIndex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AadhaarEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    PanBlindIndex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PanEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    VillageOrCity = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TalukasCsv = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BankBranch = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IfscCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpiId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AadhaarDocumentKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PanDocumentKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedCommissionRatePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ApprovedNewGpRatePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ApprovedRenewalRatePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "formula_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePeriodName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RateStartYear = table.Column<int>(type: "integer", nullable: false),
                    RateEndYear = table.Column<int>(type: "integer", nullable: false),
                    TaxDivisor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    NoticeGraceDays = table.Column<int>(type: "integer", nullable: false),
                    EarlyRebatePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    LatePenaltyPct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ApplyLightingHealthOnOpenPlot = table.Column<bool>(type: "boolean", nullable: false),
                    RoundArea = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formula_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "protected_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EncryptedContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protected_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscription_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscription_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_weightages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UsageName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Weightage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_weightages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "water_tax_masters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WaterTypeName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_water_tax_masters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_area_slab_taxes_TenantId_AreaFrom_AreaTo",
                table: "area_slab_taxes",
                columns: new[] { "TenantId", "AreaFrom", "AreaTo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_TenantId_FundType",
                table: "bank_accounts",
                columns: new[] { "TenantId", "FundType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_type_masters_TenantId_Code",
                table: "construction_type_masters",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_depreciation_slabs_TenantId_AgeFrom_AgeTo",
                table: "depreciation_slabs",
                columns: new[] { "TenantId", "AgeFrom", "AgeTo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_AadhaarBlindIndex",
                table: "field_agents",
                column: "AadhaarBlindIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_AgentCode",
                table: "field_agents",
                column: "AgentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_MobileNo",
                table: "field_agents",
                column: "MobileNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_PanBlindIndex",
                table: "field_agents",
                column: "PanBlindIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_UserId",
                table: "field_agents",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_formula_settings_TenantId_RatePeriodName",
                table: "formula_settings",
                columns: new[] { "TenantId", "RatePeriodName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_protected_assets_AssetKey",
                table: "protected_assets",
                column: "AssetKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscription_payments_TenantId_PaymentDateUtc",
                table: "tenant_subscription_payments",
                columns: new[] { "TenantId", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_weightages_TenantId_Code",
                table: "usage_weightages",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_water_tax_masters_TenantId_Code",
                table: "water_tax_masters",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_slab_taxes");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "construction_type_masters");

            migrationBuilder.DropTable(
                name: "depreciation_slabs");

            migrationBuilder.DropTable(
                name: "field_agents");

            migrationBuilder.DropTable(
                name: "formula_settings");

            migrationBuilder.DropTable(
                name: "protected_assets");

            migrationBuilder.DropTable(
                name: "tenant_subscription_payments");

            migrationBuilder.DropTable(
                name: "usage_weightages");

            migrationBuilder.DropTable(
                name: "water_tax_masters");

            migrationBuilder.DropColumn(
                name: "CensusCode",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "GramsevakMobile",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "GramsevakName",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "GramsevakSignatureKey",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "IsArrearsRolloverInitialized",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "OfficeAddress",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "OfficialStampKey",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "OperatorMobile",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "OperatorName",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "OperatorSignatureKey",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "ProvenanceJson",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "SarpanchMobile",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "SarpanchName",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "SarpanchSignatureKey",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "SetupProgressPct",
                table: "gram_panchayats");

            migrationBuilder.DropColumn(
                name: "UpSarpanchName",
                table: "gram_panchayats");
        }
    }
}
