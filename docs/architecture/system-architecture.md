# System Architecture Overview (`GPMH-SaaS-.NET`)

## 1. Modular Monolith + Strict Clean Architecture

`GPMH-SaaS-.NET` separates statutory Maharashtra Gram Panchayat math cleanly from EF Core persistence, QuestPDF rendering, external HTTP integrations, and ASP.NET Core HTTP adapters:

```text
clients/react-app (React 19.3 + Tailwind v4 + TypeScript + i18next)
        │ HTTPS JSON / Blob PDF / ProblemDetails (RFC 9457)
        ▼
src/WebAPI (ASP.NET Core, Serilog, GlobalExceptionHandler, TenantResolutionMiddleware, OpenAPI/Scalar)
        │ MediatR ISender
        ▼
src/Application (CQRS Commands/Queries, FluentValidation ValidationBehavior, Interfaces, DTOs)
        │ Implements Interfaces / Pure Domain Calls
        ├──► src/Domain (Zero Dependencies: Tax Calculator, FIFO Allocator, Partition Validator, Entities, Enums)
        └──◄ src/Infrastructure (EF Core + Npgsql RLS Interceptor, Audit Interceptor, QuestPDF, LgdDirectoryClient)
```

---

## 2. Core Request Pipeline

1. **Ingress & Observability (`WebAPI`):** Every request receives a Correlation ID, Trace ID, and Serilog diagnostic scope. Unhandled exceptions map strictly to RFC 9457 `ProblemDetails` with zero stack traces leaked in Production.
2. **Authentication & Tenant Resolution:**
   - JWT Bearer (15m lifetime) identifies `UserId`, `Role`, and optional `AgentId`.
   - `TenantResolutionMiddleware` validates any `X-Tenant-ID` header against the user's verified `TenantMembership` table (or audited `SuperAdmin` impersonation context) — returning `403 ProblemDetails` immediately on mismatch.
3. **Database Isolation (`Infrastructure` + PostgreSQL):**
   - `AppDbContext` applies Global Query Filters to every `ITenantEntity`.
   - `TenantRlsConnectionInterceptor` executes parameterized `SELECT set_config('app.current_tenant_id', @tenantId, true)` inside the PostgreSQL transaction before SQL queries execute.
   - `AuditSaveChangesInterceptor` records same-transaction `AuditLog` entries with masked secrets (`[MASKED]`), `OldValuesJson`, `NewValuesJson`, and `ChangedColumns`.
4. **External LGD Integration:**
   - `ILgdDirectoryClient` queries `https://api.grampanchayatmh.in/api/v1/` (source maintained in `external-api-proj/`) using trailing-slash-normalized `BaseAddress` and standard Microsoft HTTP resilience policies.

