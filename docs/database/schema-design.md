# Database Schema Architecture & Prisma Migration Corrections

[`canonical-entities.sql`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/contracts/db/canonical-entities.sql) resolves every structural and security flaw identified in the legacy `database/schema.prisma`:

| Defect Found in Legacy `schema.prisma` | Resolution Applied in Canonical EF Core / PostgreSQL Schema |
| :--- | :--- |
| `Float` used on `annualFee`, `openingBalance`, `VillageSubscription.amount`, `VillagePaymentSubmission.amount` | Replaced with exact fixed-point `numeric(14,2)` (`decimal` in C#) |
| Global `@unique` on `Citizen.citizenCode` and `AssessmentYear.yearLabel` (blocked multi-tenancy) | Replaced with composite `UNIQUE (tenant_id, citizen_code)` and `UNIQUE (tenant_id, year_label)` + partial unique index for one active year per GP |
| Child tables (`PropertyOwner`, `PropertyConstruction`, `PropertyTaxCalc`) & `UsageWeightage` lacked `tenant_id` | Added explicit `tenant_id` FK on every tenant and child table so PostgreSQL `FORCE ROW LEVEL SECURITY` evaluates locally without expensive joins |
| Missing `User`, `TenantMembership`, `RefreshToken`, `ReceiptCounter`, `VoucherCounter`, `PropertyCounter`, `ParentPropertyId` split lineage, `Namuna5CCashbook`, and `Agent*` tables | Added full identity/rotation tables, row-locked fiscal counters, `parent_property_id` self-reference, automatic `namuna5c_cashbook_entries`, and `field_agents` / `agent_commissions` / `agent_payouts` |
| Cleartext `aadhaarNo` storage and legacy `generalQRPath`/`waterQRPath` conflict | Aadhaar stored encrypted (`aadhaar_encrypted` + `aadhaar_blind_index` + `aadhaar_last4`); payment QRs generated dynamically from `bank_accounts.upi_id` |
| Weak `AuditLog` missing old/new diffs and mutable permissions | Added `old_values_json`, `new_values_json`, `changed_columns`, `actor_role`, plus `REVOKE UPDATE, DELETE ON audit_logs FROM gpmh_app` |

