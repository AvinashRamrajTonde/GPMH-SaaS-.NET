# Sprint 1 Prompt Playbook (`GPMH-SaaS-.NET`) — Reconciled Edition

> **Sprint 1 Issues:** `#5` (5), `#6` (5), `#7` (3), `#8` (3 — Done/Hardened), `#9` (2 — Done/Hardened), `#24` (5) = **23 Points**  
> **Baseline Status:** Issue `#5` solution scaffold (`GPMH.sln`, `Domain`, `Application`, `Infrastructure`, `WebAPI`, `ArchitectureTests`, `UnitTests`, `IntegrationTests`, `clients/react-app`, `docker-compose.yml`, `.github/workflows/ci.yml`) and hardened `external-api-proj/` (`#8/#9`) are already initialized and compiling cleanly in this repository!

---

## Prompt 1 — Complete Issue `#6`: PostgreSQL `FORCE RLS` & Membership Validation
Review [`multi-tenancy-rls.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture/multi-tenancy-rls.md) and [`canonical-entities.sql`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/contracts/db/canonical-entities.sql).

Requirements:
1. Connect EF Core `AppDbContext` (`src/Infrastructure/Persistence/`) to real Npgsql PostgreSQL with EF migrations generating the core tenant tables (`tenants`, `gram_panchayats`, `tenant_memberships`, `assessment_years`, `properties`, `citizens`).
2. Enforce `FORCE ROW LEVEL SECURITY` using **`TenantRlsConnectionInterceptor`** executing **parameterized** `SELECT set_config('app.current_tenant_id', @tenantId, true);` inside an active DB transaction (never string interpolation!). Verify role `gpmh_app` has `NOBYPASSRLS`.
3. Enforce `TenantResolutionMiddleware`: validate `X-Tenant-ID` against `ClaimsPrincipal` + active `TenantMembership`; return `403 ProblemDetails` on unauthorized tenant selection.
4. Run `CrossTenantAccessTests` against live PostgreSQL (`docker compose up -d`) proving Tenant Alpha cannot read/update/delete Tenant Beta rows.

---

## Prompt 2 — Complete Issue `#7`: Identity, 8+1 Role RBAC & Rotating Refresh Tokens
Review [`canonical-roles-and-rbac.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/canonical-roles-and-rbac.md) and `ADR-006`/`010`/`015`.

Requirements:
1. Implement password hashing (`ASP.NET Core PasswordHasher`), 15-minute JWT issuance, and 7-day rotating `RefreshToken` (`token_hash`, `family_id`, `revoked_at`, `replaced_by_token_id`).
2. On refresh-token reuse after revocation, revoke the entire `family_id` and write a security audit entry.
3. Configure dual storage (`HttpOnly; Secure; SameSite=Strict` refresh cookie for React web + JSON body token for mobile clients) and policies `RequireGramsevak`, `RequireSuperAdmin`, `RequireSurveyor`, `RequireFieldAgent`.

---

## Prompt 3 — Complete Issue `#24`: Persistent `AuditLog` & PostgreSQL Immutability Migration
Extend the existing `AuditSaveChangesInterceptor` scaffold in `src/Infrastructure/Persistence/`:
1. Persist full `OldValuesJson`, `NewValuesJson`, `ChangedColumns`, `ActorId`, `ActorRole`, `IpAddress`, and `TimestampUtc` within the same EF Core transaction.
2. Mask sensitive fields (`PasswordHash`, Aadhaar raw fields) before serializing audit JSON.
3. Apply `REVOKE UPDATE, DELETE ON TABLE audit_logs FROM gpmh_app;` in the EF migration and add an integration test proving UPDATE/DELETE throws PostgreSQL permission denied.

---

## Prompt 4 — Wire Live Onboarding Preview + End-to-End Sprint 1 QA
1. Connect `clients/react-app` LGD lookup screen to `GET /api/v1/lgd/panchayat/{code}` (or direct `https://api.grampanchayatmh.in/api/v1/`).
2. Run `.agents/workflows/verification-loop.md`: `dotnet build`, `dotnet test`, `npm run lint && npm run build`, and `bash external-api-proj/tests/test_api.sh`.

