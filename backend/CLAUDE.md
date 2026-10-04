# Backend — ASP.NET Core, Clean Architecture

.NET 10 (LTS), C# latest, nullable enabled, warnings as errors. One solution: `HRS.sln`.

## Projects and the dependency rule
Dependencies point inward only. `tests/HRS.ArchitectureTests` (NetArchTest) fails the build if a rule breaks.

| Project | Holds | May reference |
| --- | --- | --- |
| `src/HRS.Domain` | Entities, value objects, enums, domain events, domain errors, business rules | nothing (no EF, no ASP.NET, no MediatR) |
| `src/HRS.Application` | Use cases (commands/queries + handlers), validators, DTOs, interfaces for infrastructure (`IAppDbContext`, `IEmailSender`, `IFileStorage`, `IClock`, `ICurrentUser`) | Domain |
| `src/HRS.Infrastructure` | EF Core DbContext + configurations + migrations, Identity, email, S3 storage, ClamAV, PDF (QuestPDF), Excel (ClosedXML), Hangfire jobs, outbox | Application, Domain |
| `src/HRS.Api` | Minimal API endpoints, auth setup, ProblemDetails, OpenAPI, rate limiting, SignalR hub, composition root | Application, Infrastructure (DI only) |
| `src/HRS.Worker` | Hangfire server host for background jobs | Application, Infrastructure |

Tests: `tests/HRS.Domain.UnitTests`, `tests/HRS.Application.UnitTests`, `tests/HRS.Api.IntegrationTests` (Testcontainers Postgres + Redis, WebApplicationFactory), `tests/HRS.ArchitectureTests`.

## Feature folders
Application and Api are organized by module, then use case:
`HRS.Application/Features/Submissions/SubmitReport/{SubmitReportCommand.cs, SubmitReportHandler.cs, SubmitReportValidator.cs}`
`HRS.Api/Endpoints/SubmissionsEndpoints.cs` maps the module's routes.
Modules: Organization, Identity, Templates, Windows, Submissions, Review, Observer, Notifications, History, Exports, Analytics, Audit, Admin.

## Conventions
- CQRS with MediatR (or the source-generated `Mediator` package). One handler per use case. Commands change state; queries never do.
- Handlers return `Result<T>` / `Result` (no exceptions for expected failures). Errors are `Error(code, message, type)`; the API maps them to RFC 9457 ProblemDetails with a stable `code` (e.g. `WINDOW_CLOSED`, `REPORT_LOCKED`, `FORBIDDEN_OBSERVER`).
- Validation with FluentValidation via a MediatR pipeline behavior. Business invariants live in Domain methods (`report.Submit(clock, actor)`), not in handlers.
- Entities: private setters, factory methods, behavior methods that raise domain events. No anemic entities, no public setters on aggregates.
- Persistence: Application uses `IAppDbContext` (DbSets) directly; no generic repository layer. Use `AsNoTracking` + projection (`Select` into DTOs) for queries.
- Authorization: every handler that touches a report or unit calls `IAccessPolicy` (resource-based: active role assignment + routing rule + observer scope). Queries filter by the caller's scope through a shared specification. Never trust IDs from the client without a scope check (prevents IDOR).
- Concurrency: `ReportInstance` has a `RowVersion` (Postgres `xmin`); autosave requires `If-Match`. State-changing POSTs accept `Idempotency-Key`.
- Time: inject `IClock` (wraps `TimeProvider`); never call `DateTime.Now`. Store `DateTimeOffset` in UTC.
- Domain events → outbox table in the same transaction → Worker dispatches (email, in-app, audit, metrics). Never send email inside a request.
- JSON: template schemas and answers are JSONB. Numeric answers used in dashboards are also written to `report_metrics`.
- Hierarchy: `Unit.Path` is a materialized path (`nat.z03.d12.m04`) indexed for prefix queries (ltree or text_pattern_ops).
- Logging: Serilog structured logs, never log answers marked confidential, never log tokens.
- Naming: snake_case tables/columns via EFCore.NamingConventions. IDs are UUIDv7 (`Guid.CreateVersion7()`).

## Commands
- Build: `dotnet build HRS.sln` · Test: `dotnet test HRS.sln` · Format: `dotnet format HRS.sln`
- New migration: use the `ef-migration` skill.
- Run: `dotnet run --project src/HRS.Api` (API) and `dotnet run --project src/HRS.Worker` (jobs).

## Don'ts
- Don't reference Infrastructure from Application or Domain.
- Don't put business rules in endpoints or EF configurations.
- Don't return EF entities from endpoints; return DTOs.
- Don't edit a migration that has been merged to `main`; add a new one.
- Don't add a NuGet package without saying why in the PR.
