# ADR 0001 — Clean Architecture in a modular monolith

Status: accepted

## Context
One small team, one organization, a deadline-driven workload with peaks, and requirements that will keep changing (new report types, levels, routing).

## Decision
Build the backend as a single deployable ASP.NET Core app plus a worker, structured with Clean Architecture (Domain, Application, Infrastructure, Api) and organized by feature module inside Application and Api. Enforce the dependency rule with architecture tests.

## Consequences
- Simple to deploy and debug; one database transaction per use case.
- Business rules are testable without a database.
- Module boundaries (folders + no cross-module entity mutation outside events) keep extraction possible later.
- Some ceremony per use case (command, handler, validator); accepted for consistency.
