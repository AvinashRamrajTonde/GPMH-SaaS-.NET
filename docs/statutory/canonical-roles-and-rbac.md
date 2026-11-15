# Canonical RBAC & Role Matrix (Single Authoritative Standard)

Reconciles the 8-role model (`DEV_GUIDE.md` / `ADR-007`), `MOD-AGT` Field Agent Portal, and legacy Marathi labels (`सरपंच`, `वसुली कारकून`) into one strict permission taxonomy.

| Canonical Enum | Marathi Title | Tenancy Scope | Authority & Key Capabilities |
| :--- | :--- | :--- | :--- |
| **`SuperAdmin`** | राज्य सुपरॲडमिन | Cross-Tenant (HQ + Audited Impersonation) | State KPIs, GP licensing/payment approvals, KYC/Commission governance, 15-min audited impersonation |
| **`Support_Admin`** | तांत्रिक सहाय्य अधिकारी | Read/Support (Audited Impersonation) | Diagnostic support & ticket resolution; zero financial write/delete rights |
| **`Gramsevak`** | ग्रामसेवक / ग्रामविकास अधिकारी | Tenant-Scoped (`TenantId`) | Class-1 DDO: Approve assessments, mutations, splits, formula changes, receipt cancellations, digital signatures |
| **`GP_Admin`** | सरपंच / उपसरपंच (प्रशासन प्रमुख) | Tenant-Scoped (`TenantId`) | Elected GP executive: Dashboards, resolution sign-off visibility, staff oversight (no receipt cancellation bypass) |
| **`GP_Operator`** | लिपिक / कर संकलन कारकून | Tenant-Scoped (`TenantId`) | Data entry for Namuna 8/9/9-Ka/10/11, Excel bulk upload, POS receipts, daily collection (cannot void receipts) |
| **`Surveyor`** | क्षेत्रीय सर्व्हेक्षक | Tenant-Scoped (`TenantId`) | Mobile/field property measurements, 4-directional Chaturseema & geo-tagged photo capture |
| **`Auditor`** | लेखापरीक्षक (ZP / LF / CA) | Read-Only (`TenantId` / Assigned GPs) | Strictly read-only access to Namuna 8/9/10/5-C registers and full field-level `AuditLogs` |
| **`Citizen`** | नागरिक / मालमत्ताधारक | Owner-Scoped (`TenantId` + `CitizenId`) | Public/portal view of own Namuna 8 extract, online demand dues, UPI QR receipt history & PDF downloads |
| **`Field_Agent`** | क्षेत्रीय विक्री / ऑनबोर्डिंग प्रतिनिधी | Agent-Scoped (`AgentId`) | Referral GP onboarding wizard, status-gated workspace (`Pending_Kyc` → `Active`), commission ledger & payout requests |

*(UI display may render regional Marathi role badges like `सरपंच (GP_Admin)` or `कर संकलन लिपिक (GP_Operator)`, while JWT `role` claims and `.NET` authorization policies strictly use the 9 canonical values above.)*

