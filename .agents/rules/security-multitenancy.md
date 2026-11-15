---
trigger: model_decision
description: Load when designing EF Core migrations, PostgreSQL RLS policies, multi-tenant auth/RBAC, Aadhaar/PII handling, or SuperAdmin/Agent impersonation.
---

# Rule: Multi-Tenancy, PostgreSQL RLS & DPDP Security

1. **Tenant Resolution (`X-Tenant-ID`)**:
   - `X-Tenant-ID` header is ONLY a selector for users with multiple GP memberships.
   - `TenantResolutionMiddleware` MUST verify active `TenantMembership` for the authenticated `UserId` (`ClaimsPrincipal`) and return HTTP `403 ProblemDetails` if unauthorized.
2. **PostgreSQL Row-Level Security (RLS)**:
   - Every tenant-owned table (including child tables like `PropertyOwners`, `PropertyConstructions`, `PropertyTaxCalculations`) MUST store `TenantId` (`uuid`) and enable `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY`.
   - Set session context safely via **parameterized** `SELECT set_config('app.current_tenant_id', @tenantId, true)` inside the connection's transaction scope (PgBouncer transaction-pooling safe — NEVER string-interpolate SQL!).
   - Application role `gpmh_app` MUST possess `NOBYPASSRLS`.
3. **Explicit Cross-Tenant / Special Context Policies**:
   - **SuperAdmin HQ & Audited Impersonation**: Uses explicit server-verified `is_superadmin` session flag or 15-minute time-limited impersonation token logged to `AuditLogs` (never ambient unrestricted queries).
   - **Field Agent (`MOD-AGT`)**: Scoped via explicit `AgentId` RLS / ownership policy rather than raw GP tenant tables.
   - **Public QR Verification (`/verify/extract/{token}`)**: Uses signed opaque token lookup via security-definer read-only view returning non-PII extract proof only.
   - **Background Jobs (FY Rollover)**: Explicitly iterate tenants and open per-tenant `SET LOCAL` scopes.
4. **Canonical 8 + 1 Business Roles**: `SuperAdmin`, `Gramsevak`, `GP_Admin` (Sarpanch), `GP_Operator` (Clerk/Tax Collector), `Surveyor`, `Citizen`, `Auditor`, `Support_Admin`, and `Field_Agent`.
5. **DPDP Act 2023 & Aadhaar/KYC Compliance**: Encrypt 12-digit Aadhaar and Agent KYC docs at rest, mask Aadhaar in APIs/UI/logs as `XXXX-XXXX-1234`, and serve uploaded KYC/property documents only through authenticated, time-limited pre-signed endpoints.

