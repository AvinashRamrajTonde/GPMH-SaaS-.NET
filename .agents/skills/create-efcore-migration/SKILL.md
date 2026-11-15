---
name: create-efcore-migration
description: Creates, reviews, and validates Entity Framework Core database migrations safely. Use whenever an entity, property, relationship, index, constraint, or database schema changes.
---

# Create a safe EF Core migration

1. Inspect the `DbContext`, entity configurations, provider, EF Core version, startup project, migration assembly, and existing migration naming convention.
2. Confirm the intended schema change and whether the table contains production data. Check tenant ownership, foreign keys, uniqueness, indexes, and precision.
3. Make the smallest model/configuration change first.
4. Generate the migration using the repository's actual command and project paths. Do not guess project names; inspect the solution.
5. Review both `Up` and `Down`, the model snapshot, generated SQL, lock/scan implications, and possible data loss.
6. For live systems, prefer expand-and-contract. Do not combine a destructive rename/drop with code deployment that immediately requires the new schema.
7. Add a data backfill only when its source, idempotency, batching, and rollback/recovery strategy are clear.
8. Ensure monetary fields use explicit precision and tenant-aware uniqueness/relationships are preserved.
9. Test migration application against a disposable instance of the same database provider where feasible. Test upgrade from the previous schema, not only an empty database.
10. Do not apply migrations to shared or production databases without explicit approval.

## Deliverables
- Migration file and model snapshot changes.
- Relevant integration test or verification evidence.
- Summary of locks, data movement, compatibility, and rollback limitations.
