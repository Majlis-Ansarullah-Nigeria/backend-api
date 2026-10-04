# ADR 0005 — Hangfire for background jobs

Status: accepted

## Decision
Use Hangfire with PostgreSQL storage, hosted in `HRS.Worker`, for window jobs, reminders, escalations, outbox dispatch and exports. The dashboard is exposed only to SYS_ADMIN under `/api/admin/jobs`.

## Consequences
- Persistent, retryable jobs without another broker.
- Jobs must be idempotent (BR-WIN-08); each uses a natural key (window id + job type + date) to avoid duplicates.
