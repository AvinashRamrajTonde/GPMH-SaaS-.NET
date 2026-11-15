---
name: build-typescript-feature
description: Implements a frontend feature in the existing TypeScript application, including API integration, state, form validation, accessibility, and tests.
---

# Build a TypeScript frontend feature

1. Inspect package manager, framework, routing, UI library, tsconfig, API client, form/query/state libraries, lint, and test conventions.
2. Check the API/OpenAPI contract and existing generated types. Do not invent a client contract when the backend schema is available.
3. Define UI states: initial, loading, empty, success, validation error, server error, offline, and forbidden where relevant.
4. Keep API calls in the shared API layer. Reuse centralized authentication, tenant context, error handling, and retry behavior.
5. Use strict types; prefer `unknown` plus safe parsing for external data. Avoid `any`, broad casts, and duplicated DTO declarations.
6. Treat frontend checks as UX only. The backend decides permissions, calculations, tenant access, and workflow transitions.
7. Avoid placing secrets or unnecessary sensitive records in browser storage, URLs, analytics, or logs.
8. Implement responsive and accessible behavior consistent with existing components. Include keyboard operation and semantic labels.
9. Add tests for important interactions, validation, errors, permission states, and API contract assumptions.
10. Run typecheck, lint, targeted tests, and build using the repository's scripts. Report exact results.
