---
description: Verify automated test coverage across Domain tax calculators, Architecture boundaries, RLS isolation, and React UI flows.
argument-hint: "[optional project]"
---

# GPMH Test Coverage Workflow (/test-coverage)

Minimum targets:
- **Overall**: `>= 80%` line/branch coverage across `Domain` and `Application`.
- **Tax & FIFO Domain Calculators**: `100%` branch coverage (all 9 depreciation boundaries, property sub-division `±0.1%` check, zero-dues gating, Chincholi Khurd `₹66` golden case, receipt reversals).
- **Multi-Tenant Security**: Integration tests covering cross-tenant read/update/delete rejection (`403`/`404`/`0 rows`) and refresh-token reuse revocation.

Command:
```bash
dotnet test GPMH.sln --collect:"XPlat Code Coverage"
```

