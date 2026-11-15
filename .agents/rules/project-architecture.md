---
trigger: always_on
---

# Rule: Clean Architecture & Modular Monolith Boundaries

1. **Project Dependencies (Enforced by NetArchTest)**:
   - `GPMH.Domain`: Zero external NuGet or project dependencies. Contains Entities, Value Objects, Enums, Domain Events, and pure deterministic Domain Services (e.g., `CapitalValueTaxCalculator`, `FifoPaymentAllocator`).
   - `GPMH.Application`: Depends ONLY on `GPMH.Domain`. Contains CQRS Commands/Queries (`MediatR`), FluentValidation validators, pipeline behaviors, DTOs, and port interfaces (`IApplicationDbContext`, `ITenantContext`, `ILgdDirectoryClient`).
   - `GPMH.Infrastructure`: Depends on `GPMH.Application` and `GPMH.Domain`. Implements EF Core `AppDbContext`, Npgsql RLS interceptors, Audit interceptor, Identity/JWT, QuestPDF document renderers, Redis cache, and HTTP clients.
   - `GPMH.WebAPI`: Composition root, Controllers/Endpoints, middlewares, ProblemDetails handler, Serilog, Swagger/Scalar. Never imports EF Core entities directly in request/response payloads.
2. **Every Command/Paginated Query Requires a Validator**: Create `{HandlerName}Validator : AbstractValidator<T>` in the same feature folder.
3. **No Direct Cross-Tenant Leaks in Domain/Application**: Every operational entity (other than platform-global catalogs explicitly marked `IGlobalEntity`) MUST implement `ITenantEntity`.

