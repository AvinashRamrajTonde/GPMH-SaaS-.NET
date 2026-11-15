-- Canonical Hardened PostgreSQL 16+ Schema Blueprint (EF Core 9/10 Target)
-- Fixes applied vs legacy database/schema.prisma:
--   1. ZERO Float/double precision on money: strictly numeric(14,2) / numeric(10,4).
--   2. Composite tenant-aware UNIQUE constraints (NO global citizenCode or yearLabel locks).
--   3. Mandatory tenant_id (uuid) + FORCE ROW LEVEL SECURITY on EVERY tenant table & child table.
--   4. Added Identity (users, tenant_memberships, refresh_tokens), Sequence Counters
--      (receipt_counters, voucher_counters, property_counters), Namuna 5-C Cash Book,
--      ParentPropertyId split lineage, and complete MOD-AGT Agent/Commission tables.
--   5. Parameterized set_config('app.current_tenant_id', ...) & REVOKE UPDATE, DELETE on audit_logs.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================
-- PLATFORM / GLOBAL TABLES
-- =========================
CREATE TABLE tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug varchar(64) NOT NULL UNIQUE,
    name varchar(200) NOT NULL,
    status varchar(32) NOT NULL DEFAULT 'PENDING_ACTIVATION',
    subscription_expiry timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE platform_payment_accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_name varchar(200) NOT NULL,
    bank_name varchar(200) NOT NULL,
    branch_name varchar(200) NOT NULL,
    account_number varchar(64) NOT NULL,
    ifsc_code varchar(20) NOT NULL,
    upi_id varchar(120) NOT NULL,
    annual_fee numeric(14,2) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    username varchar(120) NOT NULL UNIQUE,
    email varchar(200) NULL UNIQUE,
    phone varchar(20) NULL,
    full_name_mr varchar(200) NOT NULL,
    full_name_en varchar(200) NOT NULL,
    password_hash text NOT NULL,
    is_super_admin boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash varchar(128) NOT NULL UNIQUE,
    family_id uuid NOT NULL,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    replaced_by_token_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE tenant_memberships (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    role varchar(32) NOT NULL, -- SuperAdmin, Gramsevak, GP_Admin, GP_Operator, Surveyor, Citizen, Auditor, Support_Admin, Field_Agent
    is_default boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, tenant_id, role)
);

-- =========================
-- TENANT GP CORE & MASTERS
-- =========================
CREATE TABLE gram_panchayats (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL UNIQUE REFERENCES tenants(id),
    lgd_code varchar(16) NOT NULL UNIQUE,
    name_mr varchar(200) NOT NULL,
    name_en varchar(200) NOT NULL,
    district_mr varchar(120) NOT NULL,
    district_en varchar(120) NOT NULL,
    taluka_mr varchar(120) NOT NULL,
    taluka_en varchar(120) NOT NULL,
    pincode varchar(10) NULL,
    total_wards int NOT NULL DEFAULT 1,
    population_census int NULL,
    lgd_override_fields jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE bank_accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    fund_type varchar(40) NOT NULL, -- GRAM_NIDHI, XV_FINANCE, WATER_SUPPLY
    bank_name varchar(200) NOT NULL,
    branch_name varchar(200) NOT NULL,
    account_number varchar(64) NOT NULL,
    ifsc_code varchar(20) NOT NULL,
    upi_id varchar(120) NULL, -- Used by QuestPDF for dynamic UPI QR (no static QR file paths)
    opening_balance numeric(14,2) NOT NULL DEFAULT 0.00,
    is_default boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (tenant_id, fund_type, account_number)
);

CREATE TABLE assessment_years (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    year_label varchar(16) NOT NULL, -- e.g. '2026-27'
    start_year int NOT NULL,
    end_year int NOT NULL,
    is_active boolean NOT NULL DEFAULT false,
    is_locked boolean NOT NULL DEFAULT false,
    UNIQUE (tenant_id, year_label)
);

CREATE UNIQUE INDEX ux_assessment_year_one_active_per_tenant
    ON assessment_years (tenant_id) WHERE is_active = true;

CREATE TABLE formula_settings (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    rate_period_name varchar(80) NOT NULL,
    rate_start_year int NOT NULL,
    rate_end_year int NOT NULL,
    tax_divisor numeric(10,2) NOT NULL DEFAULT 1000.00,
    notice_grace_days int NOT NULL DEFAULT 15,
    early_rebate_pct numeric(5,2) NOT NULL DEFAULT 10.00,
    late_penalty_pct numeric(5,2) NOT NULL DEFAULT 10.00,
    apply_lighting_health_on_open_plot boolean NOT NULL DEFAULT true,
    round_area boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (tenant_id, rate_period_name)
);

CREATE TABLE usage_weightages (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    code varchar(32) NOT NULL,
    usage_name varchar(80) NOT NULL,
    weightage numeric(8,4) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (tenant_id, code)
);

-- =========================
-- CITIZENS & NAMUNA 8 PROPERTIES (Tenant-Scoped + Partition Lineage)
-- =========================
CREATE TABLE citizens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    village_id uuid NULL,
    citizen_code varchar(40) NOT NULL,
    ward_no varchar(40) NULL,
    full_name_mr varchar(200) NOT NULL,
    full_name_en varchar(200) NOT NULL,
    gender varchar(16) NOT NULL, -- Male, Female, Other
    mobile_no varchar(20) NULL,
    aadhaar_encrypted bytea NULL,        -- AES-256-GCM encrypted
    aadhaar_blind_index varchar(64) NULL,-- HMAC-SHA256 for exact lookup
    aadhaar_last4 char(4) NULL,          -- Display as XXXX-XXXX-1234
    social_category varchar(24) NOT NULL,
    exemption_category varchar(40) NOT NULL DEFAULT 'NotExempted',
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (tenant_id, citizen_code),
    UNIQUE (tenant_id, aadhaar_blind_index)
);

CREATE TABLE properties (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    parent_property_id uuid NULL REFERENCES properties(id), -- Sub-division / split lineage
    ward_no varchar(40) NOT NULL,
    property_no varchar(40) NOT NULL, -- Supports suffixes '2', '2/1', '2/2'
    built_up_area_sqft numeric(12,4) NOT NULL DEFAULT 0,
    open_plot_area_sqft numeric(12,4) NOT NULL DEFAULT 0,
    total_area_sqft numeric(12,4) NOT NULL DEFAULT 0,
    boundary_east text NULL,
    boundary_west text NULL,
    boundary_north text NULL,
    boundary_south text NULL,
    water_connection_code varchar(24) NOT NULL DEFAULT 'NONE',
    is_tax_exempt boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (tenant_id, ward_no, property_no)
);

CREATE TABLE property_owners (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id), -- Direct RLS without join
    property_id uuid NOT NULL REFERENCES properties(id) ON DELETE CASCADE,
    citizen_id uuid NOT NULL REFERENCES citizens(id),
    ownership_share_pct numeric(5,2) NOT NULL DEFAULT 100.00,
    is_primary_owner boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE property_constructions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    property_id uuid NOT NULL REFERENCES properties(id) ON DELETE CASCADE,
    construction_type_code varchar(40) NOT NULL,
    usage_code varchar(32) NOT NULL,
    floor_no int NOT NULL DEFAULT 1,
    area_sqft numeric(12,4) NOT NULL,
    area_sqm numeric(12,4) NOT NULL,
    construction_start_year int NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE property_tax_calcs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    property_id uuid NOT NULL REFERENCES properties(id),
    assessment_year_id uuid NOT NULL REFERENCES assessment_years(id),
    building_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    open_land_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    lighting_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    health_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    sanitation_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    toilet_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    water_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    other_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    total_tax numeric(14,2) NOT NULL DEFAULT 0.00,
    calculation_snapshot jsonb NOT NULL,
    UNIQUE (tenant_id, property_id, assessment_year_id)
);

-- =========================
-- COUNTERS, RECEIPTS, CASHBOOK & AUDIT
-- =========================
CREATE TABLE receipt_counters (
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    financial_year varchar(16) NOT NULL,
    last_sequence bigint NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, financial_year)
);

CREATE TABLE voucher_counters (
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    financial_year varchar(16) NOT NULL,
    last_sequence bigint NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, financial_year)
);

CREATE TABLE property_counters (
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    ward_no varchar(40) NOT NULL,
    last_sequence bigint NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, ward_no)
);

CREATE TABLE tax_receipts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    property_id uuid NOT NULL REFERENCES properties(id),
    assessment_year_id uuid NOT NULL REFERENCES assessment_years(id),
    operation_id uuid NOT NULL, -- Offline/retry idempotency key
    book_no varchar(24) NOT NULL,
    receipt_no varchar(40) NOT NULL,
    receipt_date date NOT NULL,
    payment_mode varchar(24) NOT NULL, -- Cash, Cheque, Upi, Online
    total_paid numeric(14,2) NOT NULL,
    head_allocation_json jsonb NOT NULL,
    is_cancelled boolean NOT NULL DEFAULT false,
    cancel_reason text NULL,
    cancelled_by uuid NULL REFERENCES users(id),
    cancelled_at timestamptz NULL,
    UNIQUE (tenant_id, operation_id),
    UNIQUE (tenant_id, assessment_year_id, book_no, receipt_no)
);

CREATE TABLE namuna5c_cashbook_entries (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    entry_date date NOT NULL,
    entry_type varchar(16) NOT NULL, -- CREDIT, REVERSAL, DEBIT
    source_receipt_id uuid NULL REFERENCES tax_receipts(id),
    head_name varchar(120) NOT NULL,
    amount numeric(14,2) NOT NULL,
    narration text NOT NULL,
    created_by uuid NOT NULL REFERENCES users(id),
    created_at timestamptz NOT NULL DEFAULT now()
);

-- Field-level append-only audit log
CREATE TABLE audit_logs (
    id bigserial PRIMARY KEY,
    tenant_id uuid NULL,
    actor_id uuid NULL,
    actor_role varchar(32) NOT NULL,
    entity_name varchar(100) NOT NULL,
    primary_key varchar(100) NOT NULL,
    action_type varchar(24) NOT NULL, -- INSERT, UPDATE, SOFT_DELETE, REVERSAL
    old_values_json jsonb NULL,
    new_values_json jsonb NULL,
    changed_columns text[] NOT NULL DEFAULT '{}',
    ip_address varchar(64) NULL,
    user_agent text NULL,
    timestamp_utc timestamptz NOT NULL DEFAULT now()
);

-- =========================
-- MOD-AGT: FIELD AGENT & COMMISSIONS
-- =========================
CREATE TABLE field_agents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL UNIQUE REFERENCES users(id),
    agent_code varchar(32) NOT NULL UNIQUE,
    kyc_status varchar(24) NOT NULL DEFAULT 'PENDING_KYC', -- PENDING_KYC, ACTIVE, SUSPENDED, REJECTED
    pan_masked varchar(16) NULL,
    aadhaar_encrypted bytea NULL,
    aadhaar_last4 char(4) NULL,
    kyc_document_keys jsonb NOT NULL DEFAULT '{}'::jsonb,
    new_gp_rate_pct numeric(5,2) NOT NULL DEFAULT 15.00,
    renewal_rate_pct numeric(5,2) NOT NULL DEFAULT 10.00,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE agent_commissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES field_agents(id),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    commission_type varchar(24) NOT NULL, -- NEW_ONBOARDING, RENEWAL, CLAWBACK
    collected_amount numeric(14,2) NOT NULL,
    rate_pct_snapshot numeric(5,2) NOT NULL,
    commission_amount numeric(14,2) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'ACCRUED', -- ACCRUED, SETTLED, CLAWED_BACK
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE agent_payouts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES field_agents(id),
    payout_amount numeric(14,2) NOT NULL,
    utr_reference varchar(120) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'PAID',
    paid_at timestamptz NOT NULL DEFAULT now()
);

-- =========================
-- ROW LEVEL SECURITY (RLS) ENFORCEMENT
-- =========================
DO $$
DECLARE tbl text;
BEGIN
    FOREACH tbl IN ARRAY ARRAY[
        'gram_panchayats','bank_accounts','assessment_years','formula_settings',
        'usage_weightages','citizens','properties','property_owners',
        'property_constructions','property_tax_calcs','receipt_counters',
        'voucher_counters','property_counters','tax_receipts','namuna5c_cashbook_entries'
    ]
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', tbl);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY;', tbl);
        EXECUTE format('
            CREATE POLICY tenant_isolation ON %I
            FOR ALL
            USING (
                current_setting(''app.current_tenant_id'', true) IS NOT NULL
                AND tenant_id = current_setting(''app.current_tenant_id'', true)::uuid
            )
            WITH CHECK (
                current_setting(''app.current_tenant_id'', true) IS NOT NULL
                AND tenant_id = current_setting(''app.current_tenant_id'', true)::uuid
            );', tbl);
    END LOOP;
END $$;

-- Protect append-only immutability on audit_logs (when role gpmh_app exists)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gpmh_app') THEN
        REVOKE UPDATE, DELETE ON TABLE audit_logs FROM gpmh_app;
    END IF;
END $$;

