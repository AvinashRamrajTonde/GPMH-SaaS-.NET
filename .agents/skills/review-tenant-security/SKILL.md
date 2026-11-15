---
name: review-tenant-security
description: Performs a focused multi-tenant SaaS security review of code, APIs, queries, exports, files, caches, jobs, and integrations.
---

# Multi-tenant security review

## Review method
1. Map the authenticated actor, tenant-resolution mechanism, membership validation, roles/permissions, and data ownership.
2. Trace each operation from HTTP/job entry to persistence and outbound side effects.
3. Inspect direct ID lookups, joins, child relationships, search, pagination, reports, exports, file storage, cache keys, background jobs, and webhooks.
4. Attempt to reason through tenant A accessing tenant B using guessed IDs, cross-tenant parent references, stale membership, tenant switching, retries, and cache collisions.
5. Verify server-side permission checks. UI checks do not count.
6. Inspect logs, audit payloads, errors, and telemetry for secrets or unnecessary personal data.
7. Check idempotency, concurrency, rate limits, upload validation, and export safety where relevant.

## Findings format
For every finding, report:
- Severity: Critical / High / Medium / Low / Informational.
- File and line reference (or the specific missing control).
- Exploit scenario and affected tenant/data boundary.
- Concrete remediation.
- Test that would prevent regression.

Do not modify code unless the parent task explicitly asks for remediation. Distinguish verified issues from items that need infrastructure/business confirmation. Do not issue a blanket security certification.
