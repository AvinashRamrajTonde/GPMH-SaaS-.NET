---
name: add-dotnet-feature
description: Implements a backend business feature in the existing ASP.NET Core SaaS solution. Use for new modules, commands, queries, domain behavior, and end-to-end backend feature work.
---

# Add a .NET backend feature

## 1. Understand before coding
1. Inspect the solution, target framework, dependency direction, feature/module layout, naming, DI, validation, endpoint, persistence, authorization, and test patterns.
2. Read applicable `.agents/rules/*.md` guidance and relevant domain documentation/ADRs.
3. Trace the existing request path from endpoint to application use case to domain to persistence.
4. Identify tenant ownership, permission, audit, idempotency, concurrency, and data migration implications.
5. For non-trivial work, write a short plan and list assumptions before editing.

## 2. Design the use case
1. Define the actor and permission.
2. Define input/output contracts and validation rules.
3. State the business invariants and workflow transitions.
4. Decide whether the operation is a command or query.
5. Define transactional boundaries and external side effects.
6. Decide how retries and concurrent updates behave.
7. Keep module boundaries intact; do not add a cross-module database dependency.

## 3. Implement in the repository's established style
- Add domain types/rules only when they represent actual business concepts.
- Add an application use case/handler and focused interfaces for external I/O.
- Add infrastructure implementations and EF configuration only where needed.
- Add thin endpoint/controller mapping and explicit DTOs.
- Enforce tenant scope and authorization server-side.
- Add audit events for sensitive actions.
- Add migrations only when the schema changes; inspect generated SQL and migration code.
- Avoid unnecessary abstractions, packages, and unrelated refactors.

## 4. Test
- Domain invariants and validation boundaries.
- Success, not found, forbidden, unauthorized, and invalid input.
- Cross-tenant access denial.
- Duplicate/retry behavior for write operations.
- Concurrency and transaction behavior where applicable.
- Integration tests for persistence/API behavior.

## 5. Verify and report
Run formatter/analyzers and the narrowest relevant tests, then build the affected projects. Report exact commands, results, files changed, migration notes, assumptions, and remaining risks. Never claim unrun tests passed.
