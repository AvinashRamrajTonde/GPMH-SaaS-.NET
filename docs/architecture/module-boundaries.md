# Clean Architecture Module & Dependency Boundaries

Enforced automatically on every build & CI run by **`tests/GPMH.ArchitectureTests`** (`NetArchTest.Rules`):

| Project | Folder | Permitted References | Forbidden References | Primary Responsibilities |
| :--- | :--- | :--- | :--- | :--- |
| **`GPMH.Domain`** | `src/Domain` | *None (Pure .NET BCL only)* | `Application`, `Infrastructure`, `WebAPI`, EF Core, MediatR, ASP.NET, QuestPDF | Entities (`ITenantEntity`, `IGlobalEntity`), `ApplicationRole`, Value Objects, `CapitalValueTaxCalculator`, `FifoPaymentAllocator`, `PropertyPartitionValidator` |
| **`GPMH.Application`** | `src/Application` | `GPMH.Domain`, `MediatR`, `FluentValidation` | `Infrastructure`, `WebAPI`, `Npgsql`, `QuestPDF`, ASP.NET HTTP primitives | Use-case Commands & Queries, `ValidationBehavior`, `IApplicationDbContext`, `ITenantContext`, `ILgdDirectoryClient`, DTOs |
| **`GPMH.Infrastructure`** | `src/Infrastructure` | `GPMH.Application`, `GPMH.Domain` | `GPMH.WebAPI` | `AppDbContext`, `TenantRlsConnectionInterceptor`, `AuditSaveChangesInterceptor`, `LgdDirectoryClient`, QuestPDF document generators, Redis/S3 adapters |
| **`GPMH.WebAPI`** | `src/WebAPI` | `GPMH.Application`, `GPMH.Infrastructure` (DI composition root only) | Direct `AppDbContext` injection inside Controllers / Endpoints | Minimal APIs / Controllers dispatching `ISender`, Middleware (`TenantResolutionMiddleware`, `GlobalExceptionHandler`), Auth setup, Serilog |

---

## Business Module Ownership Map (Across `Domain` / `Application` / `Infrastructure`)

1. **Tenancy & Onboarding (`Tenancy`):** `Tenant`, `GramPanchayat`, `TenantMembership`, `AssessmentYear`, LGD auto-fill sync, default master seeding.
2. **Masters & Tax Formula (`Masters`):** 12 `ConstructionType` rates, 3 `UsageWeightage`, 9 `DepreciationSlab`, `AreaSlabTax`, `WaterTaxRate`, `FormulaSetting`.
3. **Namuna 8 & Mutation (`Assessments`):** `Property`, `PropertyFloor`, `PropertyMutation`, `PropertySplit`, `PropertyCounter`, `Namuna 8` 29-col QuestPDF.
4. **Demand & Billing (`Billing`):** `PropertyDemand`, `Namuna 9` 2-page register, `Namuna 9-Ka` bills + UPI QR + 15-day notice.
5. **Treasury & Receipts (`Collections`):** `TaxReceipt` (`Namuna 10`), `MiscReceipt` (`Namuna 11`), `ReceiptCounter`, `Namuna5CCashBookEntry`, strict FIFO allocator & reversal workflow.
6. **Citizens & Bulk Migration (`Citizens`):** Encrypted/masked Aadhaar registry, Citizen 360°, 26-column Excel import, public signed-token extract verifier.
7. **SuperAdmin & Field Agents (`Admin`, `Agents`):** Subscription/UTR license checks, 15m audited impersonation, `FieldAgent`, KYC review, commissions & UTR payout settlement.
8. **Offline Tax Camp Sync (`Sync`):** Pre-allocated `Namuna 6` receipt-book serial ranges and idempotent `OperationId` replay queue.

