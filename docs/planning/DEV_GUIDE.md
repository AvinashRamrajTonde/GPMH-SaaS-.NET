# e-Grampanchayat ERP (`GPMH-SaaS-.NET`) — Developer & AI Agent Guide (Canonical)
## Phase 1 Scope: Statutory Namuna 8, 9, 9-क, 10, 5-C Sync, Onboarding, SuperAdmin & Agent Portal

---

## 1. Locked Technical & Domain Stack

| Layer | Technology |
| :--- | :--- |
| **Backend Architecture** | ASP.NET Core Clean Architecture (`src/Domain`, `Application`, `Infrastructure`, `WebAPI`), MediatR, FluentValidation, RFC 9457 `ProblemDetails`, Serilog (`net9.0` local SDK baseline, ready for `net10.0` switch in `Directory.Build.props`) |
| **Frontend SPA** | React 19.3 + Vite + TypeScript + Tailwind CSS v4 + TanStack Query + React Router + `react-i18next` (`mr`, `hi`, `en`, Noto Sans Devanagari) in `clients/react-app` |
| **Database & Tenancy** | PostgreSQL 16+ + EF Core + Npgsql; `TenantId` (`uuid`) on all operational/child tables + parameterized `set_config('app.current_tenant_id', @tid, true)` `FORCE ROW LEVEL SECURITY` (`gpmh_app` with `NOBYPASSRLS`) |
| **PDF Engine** | **QuestPDF** server-side vector generation (`Namuna 8` 29-col A4 Landscape, `Namuna 9-Ka` 2-in-1 A4 Landscape/A5 with dynamic UPI QR, `Namuna 10 & 11` Dual A4 Landscape `138/8/138mm`) |
| **External LGD API** | Live Directory Service: `https://api.grampanchayatmh.in/api/v1/` (Hardened source in `external-api-proj/`, 27,931 GPs) |

---

## 2. Statutory Formula Truth (No Ambiguity)

Full breakdown in [`docs/statutory/canonical-tax-specification.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/canonical-tax-specification.md):
- **Capital Value & Tax Formula (`TaxDivisor = 1000`):**
  $$\text{Building Tax} = \text{round}\!\left(\frac{(\text{AreaSqm} \times \text{LandRate} + \text{AreaSqm} \times \text{RRRate} \times \text{DepPct}/100) \times \text{UsageWeight} \times \text{TaxRate}}{1000}\right)$$
- **Chincholi Khurd Golden Vector:** `20.90 sqm`, Land `760`, RR `5852`, Age `5` (`<= ageTo` → `95%`), Usage `1.0`, TaxRate `0.5`, Divisor `1000` ⇒ **`₹66` Building Tax** (`+ ₹180 slabs = ₹246`).
- **Default Seeds:** Usage `(1.0, 1.2, 1.25)`, Slabs `1–300: 40/40/100`, Water `0/720/720`, Notice **15 days**, Rebate/Penalty **10%**, FIFO **`Notice/Penalty -> Oldest Arrears -> Current Year`**.

---

## 3. Reconciled 6-Sprint Plan (`120 Points`)

See full table and dependencies in [`github-plan.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/github-plan.md):
* **Sprint 1 (`23 pts`):** `#5, #6, #7, #8, #9, #24` — Foundation, RLS, 8+1 RBAC, Audit Diffs, Hardened LGD API.
* **Sprint 2 (`22 pts`):** `#10, #11, #12, #25, #26, #30` — Onboarding Wizard, Overrides, Dynamic UPI Bank Config, Dashboard, Direct Provisioning, Agent KYC Register.
* **Sprint 3 (`22 pts`):** `#13, #14, #15, #16, #17, #27` — Formula Masters, Namuna 8 Tax Engine, 29-col A4 Landscape QuestPDF, Mutations & Partition Split.
* **Sprint 4 (`10 pts`):** `#18, #19` — Namuna 9 Two-Page Register & Namuna 9-Ka Dynamic QR Demand Bills.
* **Sprint 5 (`21 pts`):** `#20, #21, #22, #23, #28, #29` — Namuna 10 FIFO POS Collection, Dual Receipt QuestPDF, SuperAdmin HQ & Agent Payouts. *(Corrected from previous typo "23 Points".)*
* **Sprint 6 (`22 pts`):** `#31, #32, #33, #34, #35` — Namuna 5-C/6 Cash Book Sync, Citizen 360° & 26-Col Excel Bulk Import, Namuna 11 & QR Plates, Offline Tax Camp Sync & Public QR Verify, FY Rollover & UPI Reconciliation.

---

## 4. Environment Configuration (Safe Redacted Template)

Always copy `.env.example` → `.env` locally (git-ignored). Never commit real credentials or tokens:

```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=gpmh_prod;Username=gpmh_app;Password=<FROM_SECRET_MANAGER>
ConnectionStrings__Redis=<FROM_SECRET_MANAGER>
JwtSettings__Secret=<MIN_32_BYTE_KMS_SECRET>
Security__PiiEncryptionKeyBase64=<32_BYTE_KMS_KEY>
ExternalServices__LgdApiBaseUrl=https://api.grampanchayatmh.in/api/v1/
```

*(Note the mandatory trailing `/` on `ExternalServices__LgdApiBaseUrl` so .NET `HttpClient` preserves `/api/v1/` on relative endpoint paths.)*

---

## 5. Pre-Configured External LGD Client Contracts

```csharp
// Always normalize trailing slash before issuing relative HTTP requests:
builder.Services.AddHttpClient<ILgdDirectoryClient, LgdDirectoryClient>(client =>
{
    var raw = configuration["ExternalServices:LgdApiBaseUrl"] ?? "https://api.grampanchayatmh.in/api/v1/";
    client.BaseAddress = new Uri(raw.EndsWith('/') ? raw : raw + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
```

Sprint 1 execution prompts: [`sprint1-prompts.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/sprint1-prompts.md).

