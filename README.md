# e-Grampanchayat ERP (`GPMH-SaaS-.NET`)

Production multi-tenant SaaS platform built for **27,931+ Gram Panchayats in Maharashtra**, adhering to the **Maharashtra Village Panchayats Act, 1958** and **Maharashtra Village Panchayats Accounts Code, 2011** (Phase 1: Statutory Namuna 8, 9, 9-क, 10, Downstream 5-C/6 Cash Book Sync, Property Sub-Division/Mutation, SuperAdmin HQ, Field Agent Portal, Citizen 360° & Audit Trail).

---

## Quick Architecture & Stack Summary

| Layer | Technology |
| :--- | :--- |
| **Backend API** | ASP.NET Core Clean Architecture (`net9.0` local SDK baseline, upgradeable via single toggle to `net10.0` LTS), MediatR CQRS, FluentValidation, Serilog, OpenAPI/Scalar |
| **Database & Tenancy** | PostgreSQL 16+ + EF Core + Npgsql; Dual isolation via EF Core Global Query Filters + PostgreSQL `FORCE ROW LEVEL SECURITY` (`app.current_tenant_id`) |
| **Print & Statutory PDFs** | QuestPDF (`29-column Namuna 8` A4 Landscape, `Namuna 10 & 11` Dual A4 Landscape, `Namuna 9-Ka` 2-in-1 A4 Landscape / A5) |
| **Frontend SPA** | React 19 + Vite + TypeScript + Tailwind CSS v4 + `react-i18next` (`mr`, `hi`, `en`, Noto Sans Devanagari) in `clients/react-app` |
| **External LGD Directory** | Public read-only Maharashtra GP Directory API ([external-api-proj/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/external-api-proj/README.md), live at `https://api.grampanchayatmh.in/api/v1/`) |

---

## Repository Map

* [AGENTS.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/AGENTS.md) & [.agents/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/.agents) — Unified `.NET` + React 19 + Maharashtra GP rules, agents, skills, and verification workflows.
* [docs/planning/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning) — Single-source reconciled [DEV_GUIDE.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/DEV_GUIDE.md), [github-plan.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/github-plan.md), and [sprint1-prompts.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/sprint1-prompts.md).
* [docs/statutory/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory) — Locked [canonical-tax-specification.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/canonical-tax-specification.md), [canonical-roles-and-rbac.md](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/canonical-roles-and-rbac.md), and [statutory-master-seeds.json](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/statutory-master-seeds.json).
* [docs/architecture/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture) — Multi-tenancy RLS spec and ADRs `ADR-001` through `ADR-017` (including Offline Sync & DPDP/Aadhaar protection).
* [contracts/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/contracts) — Hardened relational DDL + composite tenant constraints (`canonical-entities.sql`) and TypeScript DTO contracts (`canonical-contracts.ts`).
* [templates/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/templates) — Deduplicated canonical HTML prototypes (`templates/html/`) and QuestPDF print specifications.
* [src/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/src) & [tests/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/tests) — Clean Architecture `.NET` solution scaffold (`Domain`, `Application`, `Infrastructure`, `WebAPI`, `ArchitectureTests`, `UnitTests`, `IntegrationTests`).
* [clients/react-app/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/clients/react-app) — React 19 + Tailwind v4 + TypeScript SPA with i18n (`mr`/`hi`/`en`) and live LGD client.
* [external-api-proj/](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/external-api-proj) — Hardened PHP 8.x + SQLite LGD Directory microservice (27,931 GPs).

---

## Local Verification Commands

```bash
# 1. Start PostgreSQL 16 (with non-superuser RLS role) & Redis 7
cp .env.example .env
docker compose up -d

# 2. Build & test .NET backend
dotnet build GPMH.sln
dotnet test GPMH.sln

# 3. Build React SPA
cd clients/react-app && npm install && npm run build

# 4. Run LGD Directory API test suite
bash external-api-proj/tests/test_api.sh
```

