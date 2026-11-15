---
trigger: glob
globs: "**/Controllers/*.cs, **/Endpoints/*.cs, **/*Request.cs, **/*Response.cs"
description: HTTP API design, validation, compatibility, and OpenAPI contract rules.
---

# API contract standards

- Follow existing routing and versioning conventions; if none exist, propose a versioning convention in an ADR before establishing a public contract.
- Use resource-oriented routes and appropriate HTTP verbs/status codes. Do not encode business actions as arbitrary GET requests.
- Validate all client input at the API/application boundary. Treat client-side validation as UX only.
- Use explicit DTOs and stable identifiers. Never expose persistence entities, internal IDs, secrets, or fields not required by the client.
- Return consistent `ProblemDetails` (or the existing standard error envelope) with a stable error code suitable for frontend handling.
- Add pagination, filtering, and sorting limits to collection endpoints. Enforce maximum page sizes server-side.
- Make retry-sensitive operations (payments, posting, imports, sync commands) idempotent with a server-validated idempotency key or equivalent design.
- Document authentication, authorization, tenant scope, validation, response schema, and error cases in OpenAPI.
- Consider backward compatibility when changing field names, enum values, requiredness, response shape, and error semantics.
- Add API integration tests for success, validation failure, unauthorized, forbidden, cross-tenant access, not found, concurrency, and duplicate requests as applicable.
