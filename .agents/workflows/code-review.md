---
description: 6-pillar code review auditing Clean Architecture, EF Core RLS isolation, statutory math accuracy, QuestPDF layout, security, and tests.
argument-hint: "[file paths | branch | commit]"
---

# GPMH Code Review Workflow (/code-review)

1. **Multi-Tenancy & RLS**: Every operational entity implements `ITenantEntity`; `X-Tenant-ID` validated via `TenantMembership`; RLS uses parameterized `set_config('app.current_tenant_id', @tenantId, true)`.
2. **Financial Accuracy**: Zero `float`/`double`; tax formula follows `(Land + DepreciatedBuilding) * UsageWeight * TaxRate / TaxDivisor(1000)` with `Age <= AgeTo`; FIFO prioritizes Notice/Penalty -> Arrears -> Current.
3. **Architecture & Validation**: Clean dependencies verified by `GPMH.ArchitectureTests`; every Command/PaginatedQuery has a FluentValidation validator; controllers never reference `DbContext`.
4. **QuestPDF & Localization**: No Puppeteer; server-side QuestPDF with embedded Noto Sans Devanagari and A4 Landscape formats; UI uses `mr`/`hi`/`en` `react-i18next` keys.
5. **Security & Audit**: Aadhaar masked (`XXXX-XXXX-1234`), `AuditLogs` append-only in the same transaction, no secrets/tokens in code or git history.
6. **Tests**: Unit + Integration + Architecture tests updated and passing.

