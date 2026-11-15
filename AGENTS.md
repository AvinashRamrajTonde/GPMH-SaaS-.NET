# GPMH Engineering Standards & AI Agent Guidelines
## e-Grampanchayat ERP (`GPMH-SaaS-.NET`) — Maharashtra Village Panchayats Act 1958 & Accounts Code 2011

You are operating in the **`GPMH-SaaS-.NET`** clean repository serving **27,931+ Gram Panchayats in Maharashtra**. Every architectural rule, statutory calculation, database constraint, and UI workflow in this repository follows **one unified baseline** (zero legacy Node/Express/Prisma references).

---

## 1. Authoritative Tech Stack

| Component | Locked Standard |
| :--- | :--- |
| **Backend Runtime & Architecture** | ASP.NET Core Clean Architecture (`src/Domain`, `src/Application`, `src/Infrastructure`, `src/WebAPI`), **MediatR + FluentValidation**, RFC 9457 `ProblemDetails`, Serilog. Local SDK target `net9.0` (upgradeable to `net10.0` in `Directory.Build.props`). |
| **Database & Multi-Tenancy** | **PostgreSQL 16+** + **EF Core + Npgsql**. Shared schema with mandatory `TenantId` (`Guid`) on every tenant-owned row + **EF Core Global Query Filters** + **PostgreSQL `FORCE ROW LEVEL SECURITY`** (`set_config('app.current_tenant_id', @tid, true)`). |
| **PDF & Print Engine** | **QuestPDF** (server-side vector PDFs). **Formats:** `Namuna 8` = **29-column A4 Landscape**; `Namuna 10` & `11` = **Dual A4 Landscape** (Office + Payer copy with perforation line); `Namuna 9-Ka` = **2-in-1 A4 Landscape / A5** with dynamic `upi://pay?pa=...` QR. |
| **Frontend SPA** | **React 19.3** + **TypeScript (strict)** + **Vite** + **Tailwind CSS v4** + **React Router** + **TanStack Query** + **`react-i18next`** (`mr`, `hi`, `en`, Noto Sans Devanagari). Lives in `clients/react-app`. |
| **External LGD Directory API** | Read-only REST API (`https://api.grampanchayatmh.in/api/v1/`, source in `external-api-proj/`). Always preserve trailing `/` on `HttpClient.BaseAddress`. |

---

## 2. Locked Statutory & Domain Truth (No Alternative Formulas Allowed)

See full spec in [`docs/statutory/canonical-tax-specification.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/canonical-tax-specification.md):
1. **Building & Capital Value Tax Formula (`TaxDivisor = 1000`):**
   - $\text{Floor Capital Value} = (\text{AreaSqm} \times \text{LandAnnualRate}) + (\text{AreaSqm} \times \text{ConstructionRRRate} \times \text{DepreciationFactor})$
   - $\text{Building Tax} = \text{round\_half\_away\_from\_zero}\!\left(\frac{\text{Floor Capital Value} \times \text{UsageWeightage} \times \text{TaxRate}}{1000}\right)$
   - Reference golden vector (Chincholi Khurd): `20.90 sqm`, Land `760`, RR `5852`, Age `5` (`<= ageTo` → `0.95`), Usage `1.0`, TaxRate `0.5`, Divisor `1000` ⇒ **`₹66` Building Tax**.
2. **Depreciation Slab Boundary Rule:** `Age = AssessmentStartYear - ConstructionStartYear`. Matching condition after Slab 1 (`0..2`) is strictly `Age > AgeFrom && Age <= AgeTo`.
3. **Default GP Formula Master Seeds:** From verified capture baseline ([`docs/statutory/statutory-master-seeds.json`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/statutory-master-seeds.json)) — 12 construction types, usage weights `(1.0, 1.2, 1.25)`, 9 depreciation slabs, 3 area slabs (`40/40/100`), 3 water slabs (`0/720/720`). All customizable per-GP in Masters.
4. **Demands, Penalties & FIFO Payment Allocation:**
   - Payment grace window: **15 days** on demand bills; **10%** early rebate / late penalty (configurable per GP policy).
   - **FIFO Allocation Order:** `Notice / Warrant / Penalty Fees → Oldest Arrears (head-wise) → Current Year Assessment → Excess Credit`.
5. **Monotonic Sequences & Reversals:** Zero gaps in `ReceiptCounter` (Namuna 10/11), `VoucherCounter` (Namuna 12), and `PropertyCounter` (split `P/1, P/2`). Receipts are **immutable**; cancellations create audited reversal postings and restore `PropertyDemand` + `Namuna 5-C Cash Book`.

---

## 3. Non-Negotiable Engineering Invariants

1. **Tenant Isolation:** Never trust client-supplied `gpId`/`TenantId` as proof of membership. Validate `X-Tenant-ID` against verified JWT claims / `TenantMembership` in `TenantResolutionMiddleware`, and always execute parameterized `SELECT set_config('app.current_tenant_id', @tenantId, true)` inside the connection transaction scope. App DB role (`gpmh_app`) MUST have `NOBYPASSRLS`.
2. **Zero Floating-Point Arithmetic for Money:** Always use C# `decimal` (stored in PostgreSQL as `numeric(14,2)` or `numeric(18,4)` for rates/weights). Never use `float`/`double` for fees, taxes, balances, or commissions.
3. **Composite Tenant Uniqueness:** Every business key (`CitizenCode`, `AssessmentYear.YearLabel`, `PropertyNo`, `ReceiptNo`) MUST use composite uniqueness `(TenantId, ...)`, never global unique constraints.
4. **Audit Immutability & PII Protection:** Append-only `AuditLog` (`REVOKE UPDATE, DELETE` on `audit_logs` for `gpmh_app`) with `OldValuesJson`, `NewValuesJson`, and `ChangedColumns`. Mask Aadhaar (`XXXX-XXXX-1234`) in UI and logs; encrypt Aadhaar/PAN at rest.
5. **Architecture Boundaries:** `Domain` has zero NuGet/project dependencies; `Application` depends only on `Domain`; `Infrastructure` depends on `Application` & `Domain`; `WebAPI` never invokes EF Core `DbContext` directly (uses MediatR). Enforced by `GPMH.ArchitectureTests`.

---

## 4. Verification Checklist (Before Any Commit or PR)

- [ ] `dotnet build GPMH.sln --configuration Release` passes with 0 warnings/errors.
- [ ] `dotnet test GPMH.sln` passes (ArchitectureTests + Tax/FIFO UnitTests + Tenant IntegrationTests).
- [ ] `cd clients/react-app && npx tsc --noEmit && npm run lint && npm run build` passes.
- [ ] `bash external-api-proj/tests/test_api.sh` passes (15/15) if LGD API was touched.
- [ ] Zero secrets, Aadhaar numbers, or hardcoded passwords in staged diff.

