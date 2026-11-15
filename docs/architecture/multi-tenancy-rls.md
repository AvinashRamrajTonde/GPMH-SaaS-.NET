# Multi-Tenancy & PostgreSQL Row-Level Security (RLS) Architecture

## 1. Dual-Layer Defense-in-Depth
1. **Application Layer (EF Core Global Query Filters):** Every entity implementing `ITenantEntity` exposes `Guid TenantId` and filters automatically via `_tenantContext.CurrentTenantId`.
2. **Database Engine Layer (PostgreSQL `FORCE ROW LEVEL SECURITY`):** Even direct SQL queries or buggy EF projections cannot cross tenant boundaries because the runtime role `gpmh_app` is provisioned with `NOSUPERUSER NOBYPASSRLS`.

## 2. Parameterized Session Context & PgBouncer Transaction Pooling
Never interpolate strings (`SET LOCAL ... = '{id}'`). Inside `TenantRlsConnectionInterceptor`:
- Every DB transaction (including read queries under explicit read transaction or scoped connection) executes:
  ```sql
  SELECT set_config('app.current_tenant_id', @tenantId, true);
  ```
- Passing `is_local = true` binds `app.current_tenant_id` strictly to the current PostgreSQL transaction so pooled PgBouncer connections never leak tenant state across requests.

## 3. Special Non-Standard Access Contexts (No Unsafe Global Bypass)
1. **SuperAdmin & Support Impersonation:** SuperAdmin analytics use dedicated security-definer/aggregated read policies or an audited 15-minute `ImpersonationToken` logged to `audit_logs` before setting `app.current_tenant_id` to the target GP.
2. **Field Agent (`Field_Agent`):** `field_agents`, `agent_commissions`, and `agent_payouts` enforce `agent_id = current_setting('app.current_agent_id', true)::uuid` instead of overloading GP `tenant_id`.
3. **Public QR Extract Verification (`/api/v1/public/verify/extract/{token}`):** Queries a signed opaque token lookup view exposing **zero Aadhaar/mobile PII** without disabling RLS on main tenant tables.
4. **Background Rollover Jobs (Hangfire / HostedService):** Must resolve active tenants cleanly via an `IGlobalEntity` query and open an isolated transaction per `TenantId` calling `set_config` for each GP batch.

