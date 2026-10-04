---
name: backend-feature
description: Add or change a backend use case in the .NET Clean Architecture solution (domain behavior, command/query, handler, validator, endpoint, authorization, tests). Use for any new API endpoint or business rule in backend/.
paths: backend/**
---

# Add a backend use case

Follow these steps in order. Templates for each file are in [templates.md](templates.md).

1. **Locate the requirement.** Find the FR/BR IDs in `docs/SRS.md` and the rules in the `hrs-domain` skill. List the acceptance criteria you will test.
2. **Domain first.** Put invariants in the aggregate as a method that returns `Result` and raises a domain event (e.g. `ReportInstance.Submit(IClock clock, ActorRef actor)`). Add unit tests in `HRS.Domain.UnitTests` for every rule and edge case (on time, late, closed, wrong state).
3. **Application.** Create `Features/<Module>/<UseCase>/` with the command or query record, handler, and FluentValidation validator. The handler: load → authorize with `IAccessPolicy` → call the domain method → `SaveChangesAsync` → map to DTO. Queries project with `Select` and `AsNoTracking`, filtered by the caller's scope.
4. **Persistence.** If the model changed, update the EF configuration in Infrastructure and create a migration with the `ef-migration` skill.
5. **Endpoint.** Map it in `HRS.Api/Endpoints/<Module>Endpoints.cs`: route under `/api/v1`, `.RequireAuthorization()`, `.WithName()`, `.Produces<T>()`, `.ProducesProblem()`. Translate `Result` errors with `ToProblemDetails()`. Add `Idempotency-Key` handling for state-changing POSTs and `If-Match` for autosave.
6. **Integration test.** In `HRS.Api.IntegrationTests`, cover the happy path, a validation failure, a 403 for a user outside scope, and a 403 for an observer when the call mutates.
7. **Contract.** Run the API and the `api-sync` skill so the frontend client stays current.
8. **Verify.** Run the `verify` skill. Report what you changed with the requirement IDs.

Checklist before you finish:
- [ ] No Infrastructure types in Domain/Application
- [ ] Errors have stable codes
- [ ] Times via `IClock`, stored UTC
- [ ] Authorization checked in the handler, scope-filtered queries
- [ ] Domain event raised for anything that should notify, audit or update metrics
