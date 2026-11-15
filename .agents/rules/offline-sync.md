---
trigger: model_decision
description: Load when implementing mobile field collection, offline-first screens, local queues, synchronization APIs, conflict resolution, or background sync.
---

# Offline-first synchronization rules

- Document which data can be read offline, which commands can be queued, and which actions require a live server decision.
- Every queued mutation must carry a stable client-generated operation/idempotency ID, entity ID, tenant context derived server-side, device/session identity, schema version, and client-observed version as appropriate.
- The server remains authoritative for permissions, validation, numbering, financial calculations, and final workflow transitions.
- Design for at-least-once delivery: duplicate and retried operations must not duplicate financial or administrative effects.
- Define conflict behavior per aggregate. Do not apply last-write-wins to money, receipts, statutory records, or other audit-sensitive data.
- Use explicit server versions/ETags or equivalent optimistic concurrency. Return actionable conflict details; do not silently overwrite newer server data.
- Use stable sync cursors and pagination. Ensure a client can resume after interruption without missing or repeatedly applying changes.
- Handle deleted records/tombstones, schema upgrades, clock skew, partial batches, expired authentication, tenant switching, and device re-registration.
- Keep the local store minimal and protect sensitive cached data according to the product's threat model.
- Add tests for duplicate, reordered, delayed, partially failed, and conflicting sync operations, plus reconnect/resume and cross-tenant attempts.
