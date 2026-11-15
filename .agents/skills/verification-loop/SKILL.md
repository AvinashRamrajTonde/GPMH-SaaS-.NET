---
name: verification-loop
description: Execute full build, architecture boundary check, unit/integration tests, TypeScript check, lint, and security scan before concluding any GPMH task.
---

# GPMH Verification Loop

Run all stages in sequence and fix every failure before reporting completion:

1. **Backend Build**: `dotnet build GPMH.sln --configuration Release` (0 errors, 0 warnings).
2. **Backend Tests**: `dotnet test GPMH.sln --no-build --configuration Release` (ArchitectureTests + UnitTests + IntegrationTests).
3. **Frontend Checks**: `cd clients/react-app && npx tsc --noEmit && npm run lint && npm run build`.
4. **LGD Directory Check** (if touched): `bash external-api-proj/tests/test_api.sh`.
5. **Security & Diff Hygiene**: Check `git status` and `git diff` for secrets, Aadhaar leaks, unparameterized SQL, or `float`/`double` monetary math.

