---
trigger: model_decision
---

# Rule: Git Version Control, Atomic Commits & PR Discipline (GPMH Standards)

Maintain a clean, audit-ready git history inside `GPMH-SaaS-.NET`.

## 1. Commit Message Conventions
`<type>(<scope>): <imperative summary>`

Allowed types: `feat`, `fix`, `refactor`, `test`, `docs`, `perf`, `chore`

Allowed scopes: `foundation`, `tenancy`, `identity`, `audit-log`, `onboarding`, `formula`, `namuna-8`, `namuna-9`, `namuna-9ka`, `namuna-10`, `namuna-5c`, `mutation`, `citizen`, `superadmin`, `agent-portal`, `questpdf`, `lgd-api`.

Examples:
- `feat(tenancy): enforce parameterized set_config RLS and TenantMembership check (closes #6)`
- `fix(formula): apply taxDivisor=1000 and Land+Depreciated Building capital value`
- `test(namuna-10): add concurrent receipt sequence lock and FIFO arrears tests`

## 2. Branching Strategy
- `main`: Always compiling and passing all tests.
- Feature branches: `feature/<scope>-<description>` (e.g., `feature/sprint-1-foundation`).
- Bugfix branches: `fix/<scope>-<description>`.

## 3. Pre-Commit Verification Checklist
- [ ] `dotnet build GPMH.sln --configuration Release` passes cleanly.
- [ ] `dotnet test GPMH.sln --no-build --configuration Release` passes with 0 failures.
- [ ] `cd clients/react-app && npx tsc --noEmit && npm run lint && npm run build` passes.
- [ ] Zero `.env`, real JWT secrets, PostgreSQL passwords, or raw citizen Aadhaar values are staged.

