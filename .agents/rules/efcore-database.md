---
trigger: glob
globs: "**/*DbContext*.cs, **/Migrations/*.cs, **/*.sql"
description: Entity Framework Core, relational schema, and migration safety rules.
---

# Persistence and EF Core rules

- Inspect the existing provider and EF Core version before using provider-specific APIs. Do not assume PostgreSQL, SQL Server, or another provider without repository evidence.
- Use migrations as the only normal schema-evolution path. Review generated migration and model snapshot; do not blindly accept destructive operations.
- Prefer expand-and-contract changes for live systems: add compatible schema, deploy code that supports both shapes, backfill, then remove obsolete schema in a later release.
- Never drop/rename populated columns or tables in the same deployment as code that stops using them unless an approved maintenance plan explicitly allows downtime/data loss.
- Configure precision for all money/rate/quantity columns and add appropriate unique constraints, foreign keys, and indexes.
- Every tenant-owned table must have a clear tenant ownership strategy and tenant-aware keys/constraints. Review foreign keys and uniqueness across tenant boundaries.
- Apply tenant scoping in a way that is difficult to bypass. Query filters are defense-in-depth, not a substitute for authorization and explicit tenant-context validation.
- Use `AsNoTracking` for read-only queries where appropriate. Project to DTOs rather than loading large object graphs.
- Avoid unbounded `Include`, N+1 queries, client-side filtering of large datasets, and loading an entire table to paginate in memory.
- Use optimistic concurrency for mutable records where concurrent edits are possible. Handle concurrency exceptions as a business outcome.
- Keep `DbContext` lifetimes scoped and short. Do not share one context across concurrent tasks.
- Do not execute database calls from inside `OnModelCreating`; keep model configuration deterministic.
- Add integration tests against the actual relational provider for constraints, transactions, query filters, and migrations when feasible.
