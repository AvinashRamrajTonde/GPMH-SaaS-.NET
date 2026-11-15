---
name: implement-offline-sync
description: Designs or implements offline-first mobile/web data capture, queued mutations, synchronization APIs, idempotency, cursoring, and conflict resolution.
---

# Implement offline synchronization

1. Read the domain aggregate, API contract, mobile/local-storage implementation, and `.agents/rules/offline-sync.md`.
2. Define the offline capability matrix: what can be read, created, edited, queued, or must wait for connectivity.
3. Define the mutation envelope: operation ID, entity ID, schema version, client-observed version, device/session metadata, and payload. Derive tenant authorization on the server.
4. Define idempotency and deduplication storage. A retry must return the original outcome or a safe equivalent, not repeat the side effect.
5. Define per-aggregate conflict policy. Do not use last-write-wins for receipts, tax demand, accounting, statutory records, or audited data.
6. Define server change feed, stable cursor, pagination, tombstones, retention, and resume behavior.
7. Define validation and authorization at sync time; permissions may have changed since the device went offline.
8. Define partial-batch semantics. The client must know which operations succeeded, failed validation, conflicted, or need retry.
9. Protect local cached data and handle logout, tenant switch, device loss, schema upgrade, and expired credentials.
10. Test duplicate, reordered, delayed, partial, and conflicting requests; resume after interruption; cross-tenant attempts; and version upgrades.
11. Document operational metrics: queue age, retry count, conflict count, sync latency, and dead-letter/failure handling.
