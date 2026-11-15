---
trigger: glob
globs: "**/*Tests*.cs, **/Tests/**/*.cs, clients/react-app/**/*.test.*"
---

# Rule: Testing & Statutory Verification Standards (GPMH .NET)

1. **Test Suites**:
   - `GPMH.ArchitectureTests` (NetArchTest): Enforces Clean Architecture layer rules, `ITenantEntity` coverage, and validator conventions.
   - `GPMH.UnitTests` (xUnit): Golden test vectors proving Chincholi Khurd (`20.90 sqm → ₹66`), depreciation `age <= ageTo` boundaries, zero-dues + `±0.1%` area conservation property split, and FIFO payment allocation.
   - `GPMH.IntegrationTests` (xUnit + `WebApplicationFactory` + PostgreSQL RLS verification): Cross-tenant isolation (`Tenant Alpha` vs `Tenant Beta` -> 403/404/0 rows), JWT refresh rotation & reuse revocation, immutable audit diffs, and live/mocked LGD API normalization.
2. **Zero Floating-Point Drift**: All money assertions compare exact `decimal` values (`Assert.Equal(66m, result.BuildingTax)`).
3. **Mandatory Pre-Commit Gate**:
   ```bash
   dotnet build GPMH.sln --configuration Release
   dotnet test GPMH.sln --no-build --configuration Release
   cd clients/react-app && npx tsc --noEmit && npm run lint && npm run build
   ```

