---
description: Automated verification pipeline covering .NET build, NetArchTest, xUnit, TypeScript check, ESLint, Vite build, and LGD API tests.
argument-hint: "[optional scope]"
---

# GPMH Verification Loop (/verification-loop)

Run end-to-end verification in order:
```bash
dotnet build GPMH.sln --configuration Release
dotnet test GPMH.sln --no-build --configuration Release
cd clients/react-app && npx tsc --noEmit && npm run lint && npm run build && cd ../..
bash external-api-proj/tests/test_api.sh
```
Check `git status` and `git diff` to confirm zero uncommitted secrets, Aadhaar leaks, or unparameterized SQL before committing.

