# Architecture Decision Records (`ADR-001` – `ADR-017`)

Unified architecture decisions for `GPMH-SaaS-.NET` (superseding legacy Node.js/Express/Prisma/Puppeteer assumptions):

- **ADR-001 – ADR-013:** Defined in [`docs/planning/github-plan.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/planning/github-plan.md) (ASP.NET Core Clean Architecture, Shared PostgreSQL `tenant_id` discriminator, Dual EF Core Query Filters + PostgreSQL `FORCE ROW LEVEL SECURITY`, Membership Validation on `X-Tenant-ID`, 15m JWT + 7d Rotating Refresh Tokens with Reuse Detection, 8+1 Role RBAC, Immutable Append-Only Audit Trail, PostgreSQL Financial Source of Truth, Dual Cookie/Keystore Auth Storage, Day-One `mr/hi/en` i18n, Agent Actor Model + Private KYC Storage, Dual-Rate Commission Engine).
- **ADR-014:** [`ADR-014-offline-camp-receipt-sync.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture/decisions/ADR-014-offline-camp-receipt-sync.md) — Offline Camp Collection Sync & Pre-Allocated Receipt Book Ranges (`Namuna 6`).
- **ADR-015:** [`ADR-015-dpdp-aadhaar-pii-protection.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture/decisions/ADR-015-dpdp-aadhaar-pii-protection.md) — DPDP Act 2023, Aadhaar AEAD Encryption & KYC Pre-Signed Access.
- **ADR-016:** [`ADR-016-canonical-tax-engine-and-seeds.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture/decisions/ADR-016-canonical-tax-engine-and-seeds.md) — Canonical Tax Formula (`TaxDivisor = 1000`), Boundary Condition (`Age <= AgeTo`) & Captured GP Seed Defaults.
- **ADR-017:** [`ADR-017-questpdf-statutory-print-standard.md`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/architecture/decisions/ADR-017-questpdf-statutory-print-standard.md) — Server-Side QuestPDF & A4 Landscape Statutory Print Geometry.

