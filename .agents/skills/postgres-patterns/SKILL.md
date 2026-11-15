---
name: postgres-patterns
description: PostgreSQL 16+ multi-tenant schema, composite uniqueness, FORCE ROW LEVEL SECURITY via parameterized set_config, monotonic row locks, and scale indexing patterns for GPMH.
---

# PostgreSQL Multi-Tenant Patterns (GPMH EF Core)

Use this skill when designing EF Core migrations, RLS policies, counters, or high-volume reporting indexes.

## 1. Mandatory Tenant & Composite Indexing Rules
- Every tenant-owned table (including child entities like `PropertyOwner`, `PropertyConstruction`, `PropertyTaxCalc`) MUST store `TenantId` (`uuid`).
- Business identifiers MUST be tenant-scoped unique indexes, NEVER globally unique:
  - `(TenantId, CitizenCode)` on `citizens`
  - `(TenantId, YearLabel)` on `assessment_years`
  - `(TenantId, WardNo, PropertyNo)` on `properties`
  - `(TenantId, AssessmentYearId, FiscalSequence)` on `tax_receipts`

## 2. Parameterized RLS Policies & PgBouncer Safety
Never interpolate `{TenantId}` directly into SQL strings. Inside `DbConnectionInterceptor` (scoped to an active transaction per request):
```sql
SELECT set_config('app.current_tenant_id', @tenantId, true);
```
Every tenant table enforces:
```sql
ALTER TABLE properties ENABLE ROW LEVEL SECURITY;
ALTER TABLE properties FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON properties
FOR ALL
USING (
    current_setting('app.current_tenant_id', true) IS NOT NULL
    AND tenant_id = current_setting('app.current_tenant_id', true)::uuid
)
WITH CHECK (
    current_setting('app.current_tenant_id', true) IS NOT NULL
    AND tenant_id = current_setting('app.current_tenant_id', true)::uuid
);
```
The application database role (`gpmh_app`) MUST be created with `NOSUPERUSER NOBYPASSRLS`.

## 3. Monotonic Sequence Locking
Statutory Receipt (`Namuna 10/11`) and Voucher (`Namuna 12`) numbers must never skip or collide under concurrency:
```sql
UPDATE receipt_counters
SET last_sequence = last_sequence + 1
WHERE tenant_id = @tenantId AND financial_year = @fy
RETURNING last_sequence;
```
Execute inside the exact same ACID database transaction as receipt creation and `Namuna 5-C` Cash Book posting.

