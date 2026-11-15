---
name: add-api-endpoint
description: Adds or modifies an ASP.NET Core HTTP API endpoint, including request/response DTOs, validation, authorization, OpenAPI, and integration tests.
---

# Add an API endpoint

1. Inspect existing routing, versioning, endpoint style, error envelope, authentication, authorization, validation, and OpenAPI setup.
2. Identify resource/action semantics, tenant scope, required permission, and whether the operation is retry-sensitive.
3. Define request and response DTOs; do not expose EF entities.
4. Validate all input at the server boundary and enforce limits on page size, sorting, filters, and payload size.
5. Keep endpoint code thin and delegate to one application use case/query.
6. Return consistent status codes and machine-readable validation/domain errors. Do not leak stack traces or internal exception messages.
7. Add idempotency for payment, posting, imports, and synchronization writes where duplicate delivery is possible.
8. Document the endpoint and error cases in OpenAPI. Generate/update the TypeScript client if the repository uses generated contracts.
9. Add integration tests for success, invalid input, authentication, authorization, cross-tenant denial, not found, duplicate requests, and concurrency as applicable.
10. Run targeted tests and report commands and outcomes.
