---
description: Restate GPMH requirements, assess statutory & multi-tenant risks, and create a phased .NET + React implementation plan before touching code.
argument-hint: "[feature description | issue number]"
---

# GPMH Planning Workflow (/plan)

1. **Statutory Truth Check**: Verify affected registers and formulas against `docs/statutory/canonical-tax-specification.md` and `docs/statutory/canonical-roles-and-rbac.md`. Flag any ambiguity rather than guessing.
2. **Layer Impact Analysis**:
   - **Domain (`GPMH.Domain`)**: Entities (`ITenantEntity`), Value Objects, Domain Events, pure calculation logic.
   - **Database / EF Core (`GPMH.Infrastructure`)**: Composite `(TenantId, ...)` unique keys, `numeric(14,2)` money types, PostgreSQL `FORCE ROW LEVEL SECURITY` migration.
   - **Application (`GPMH.Application`)**: MediatR Commands/Queries, `AbstractValidator<T>`, DTOs (masked PII), audit triggers.
   - **WebAPI (`GPMH.WebAPI`)**: Thin routes, `RequireActiveTenantMembership` + role policy, RFC 9457 `ProblemDetails`.
   - **QuestPDF & Frontend (`clients/react-app`)**: Vector A4 Landscape QuestPDF documents + strict TypeScript React 19 UI with `mr`/`hi`/`en` localization.
3. **Verify Non-Negotiables**: Parameterized `set_config` RLS, decimal arithmetic, atomic sequence counters, and immutable audit diffs before presenting plan for confirmation.

