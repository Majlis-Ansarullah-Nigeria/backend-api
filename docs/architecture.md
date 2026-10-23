# Architecture

One Next.js web app, one ASP.NET Core modular monolith built with Clean Architecture, one background worker, PostgreSQL, Redis and S3-compatible storage. Everything ships as containers deployed by Dokploy.

## Runtime view
```
Browser ──HTTPS──▶ Traefik (Dokploy)
                    ├── /          → web     (Next.js standalone, :3000)
                    ├── /api/*     → api     (ASP.NET Core, :8080)
                    └── /hubs/*    → api     (SignalR)
api, worker ──▶ PostgreSQL (Dokploy database service, daily backups to S3)
api, worker ──▶ Redis (cache, rate limits, locks)
api, worker ──▶ S3-compatible storage (attachments, exports) ── ClamAV scan
worker ──▶ email provider (SES / SendGrid / SMTP)
migrator (one-shot EF bundle) runs before api starts on each deploy
```
Same origin for web and API means cookies are first-party (SameSite=Lax/Strict) and no CORS is needed. In local dev, Next.js rewrites `/api/*` to `http://localhost:8080`.

## Clean Architecture (backend)
```
HRS.Api ──▶ HRS.Application ──▶ HRS.Domain
   │              ▲
   └──▶ HRS.Infrastructure ──┘   (implements Application interfaces)
HRS.Worker ──▶ Application + Infrastructure
```
- **Domain:** aggregates `Unit`, `RoleAssignment`, `ReportType`, `TemplateVersion`, `SubmissionWindow`, `ReportInstance`; value objects (`UnitPath`, `ReferenceNumber`, `Deadlines`); domain events; `Result`/`Error`.
- **Application:** one folder per use case (command/query + handler + validator), pipeline behaviors (validation, logging, transaction, audit), interfaces (`IAppDbContext`, `IAccessPolicy`, `IClock`, `ICurrentUser`, `IFileStorage`, `IVirusScanner`, `IEmailSender`, `IPdfRenderer`).
- **Infrastructure:** EF Core (Npgsql, snake_case, JSONB), ASP.NET Core Identity + cookie/JWT handling, outbox + dispatcher, Hangfire jobs, QuestPDF, ClosedXML, S3 client, ClamAV client.
- **Api:** minimal API endpoint groups per module, ProblemDetails, OpenAPI document at `/openapi/v1.json`, rate limiting, security headers, health checks at `/api/health`.
- **Worker:** Hangfire server: window open/remind/close/missed jobs, outbox dispatch, exports, escalations.

Enforced by `tests/HRS.ArchitectureTests`.

## Key patterns
- **Authorization:** resource-based `IAccessPolicy` + scope specification applied to every query. Roles come from time-bounded `RoleAssignment`s, not from claims alone; the active assignment is sent as a header/cookie and re-validated per request.
- **Templates:** a published `TemplateVersion.Schema` (JSONB) drives both the frontend form engine and backend validation. Answers are JSONB keyed by stable field keys. Numeric answers are copied to `report_metrics` for dashboards.
- **Expressions:** computed fields and `showIf` use a small safe expression language with two implementations (C# and TypeScript) tested against the shared fixtures in `shared/expression-fixtures.json`.
- **Outbox:** domain events are stored in `outbox_messages` in the same transaction and dispatched by the worker (notifications, metrics, webhooks).
- **Audit log:** written to `audit_events` in the same transaction as the change it records, append-only (ADR 0007).
- **Idempotency & concurrency:** `Idempotency-Key` on state-changing POSTs; `If-Match` + row version on autosave.

## Frontend
Next.js App Router; Server Components for read pages; client components for forms, builder and charts; TanStack Query over an orval-generated client; RHF + Zod; shadcn/ui + Tailwind with tokens from `docs/design/README.md`; next-intl; PWA manifest + service worker for install and offline draft queue.

## Environments
`dev` (docker compose on a laptop) · `staging` (separate Dokploy compose app from the `develop` branch, anonymized data) · `production` (Dokploy compose app from `main`).
