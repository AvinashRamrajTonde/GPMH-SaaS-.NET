# GPMH REST API & Integration Standards

## 1. Envelope & Error Standard (RFC 9457 ProblemDetails)
- **Errors:** Always return `application/problem+json` conforming to RFC 9457 (`type`, `title`, `status`, `detail`, `instance`, `traceId`, optional `errors` dictionary for validation failures). Never return HTTP 200 `{ isSuccess: false }` envelopes or raw stack traces.
- **Success:** Use direct typed DTO payloads (or paginated `PagedResult<T>` with `items`, `pageNumber`, `pageSize`, `totalCount`, `totalPages`). Enforced max `pageSize = 100`.

## 2. Routing, Tenant Header & Idempotency
- Route prefix: `/api/v1/...` (resource nouns + HTTP verbs; state transitions use explicit POST sub-resources like `/api/v1/receipts/{id}/cancel`).
- Multi-tenant requests carry `X-Tenant-ID: <guid>`, strictly verified against `TenantMembership` for the authenticated JWT `UserId`.
- Retry-sensitive mutations (tax collection, offline camp sync batches, Excel bulk imports, payout settlements, FY rollover) MUST support `Idempotency-Key` headers / stable `OperationId` UUIDs.

## 3. LGD Client Integration Safety
- Configured `ExternalServices:LgdApiBaseUrl` MUST end with a trailing slash (`https://api.grampanchayatmh.in/api/v1/`).
- Backend `LgdDirectoryClient` MUST normalize trailing slashes programmatically so relative requests (`panchayat/{code}`) never truncate `/v1/`.

