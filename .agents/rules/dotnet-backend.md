---
trigger: glob
globs: "**/*.cs, *.cs"
description: ASP.NET Core and C# implementation standards for backend source files.
---

# .NET backend standards

- First inspect the target framework and package versions in the solution. Do not upgrade them as part of an unrelated feature.
- Follow the repository's C# language version, nullable reference type, analyzers, formatting, and naming settings.
- Use async APIs for I/O and pass `CancellationToken` from the HTTP/job boundary through application and infrastructure calls. Do not wrap synchronous work in `Task.Run` to simulate asynchrony.
- Avoid `.Result`, `.Wait()`, `async void`, fire-and-forget tasks, and swallowed exceptions.
- Keep endpoint/controller code thin: bind/validate, authorize, invoke a use case, map the result, and return an HTTP response.
- Use explicit request/response DTOs. Never serialize EF entities or expose internal exception details.
- Use constructor injection and focused services. Do not use service locator patterns or inject the entire service provider into business logic.
- Enable nullable reference types and handle nullability deliberately; do not silence warnings with `!` without a documented reason.
- Use `DateTimeOffset` for instants and `DateOnly` for business dates where appropriate. Store instants in UTC; keep business timezone and local-date rules explicit.
- Use `decimal` for monetary amounts and rates. Specify database precision and rounding policy.
- Use `ProblemDetails` or the repository's consistent error envelope. Do not return success-shaped payloads for failures.
- Never catch an exception merely to log and rethrow unchanged. Add context at a meaningful boundary or let centralized exception handling process it.
- Keep business logic deterministic and unit-testable. External I/O belongs behind interfaces.
