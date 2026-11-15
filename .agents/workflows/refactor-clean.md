---
description: Safe cleanup and refactoring workflow preserving Clean Architecture boundaries and statutory tax behavior.
argument-hint: "[target module or files]"
---

# GPMH Refactor & Clean Workflow (/refactor-clean)

1. Capture passing baseline (`dotnet test GPMH.sln` + `cd clients/react-app && npm run build`).
2. Eliminate duplicate calculation helpers, dead template copies, or leaked infrastructure imports.
3. Re-run `GPMH.ArchitectureTests`, `GPMH.UnitTests`, `GPMH.IntegrationTests`, and `npm run build` to prove zero behavioral drift.

