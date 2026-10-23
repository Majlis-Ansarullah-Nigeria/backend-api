# ADR 0007 — Audit log written in the same transaction, not through the outbox

Status: accepted

## Context
`architecture.md` lists the audit log among the things the outbox dispatches. FR-AUD-001 needs every audited change to have an entry, with the client's IP and user agent, and the log to be append-only. Going through the outbox adds a hop through the worker, and by then the request (and its IP and user agent) is gone.

## Decision
- Handlers write `audit_events` rows through `IAuditLog.Add` into their own unit of work, so an entry commits or rolls back with the change it describes. Actions with no unit of work of their own (sign-ins, downloads) use `IAuditLog.RecordAsync`.
- Downloads are queries and must not change state, so the endpoint sends `RecordDownloadCommand` after a successful download.
- IP and user agent come from `IRequestContext` (HTTP in the API, empty in the worker). The API reads `X-Forwarded-For` only from proxies on `ForwardedHeaders:KnownNetworks`.
- `audit_events` is append-only: no domain mutators, and a database trigger rejects UPDATE, DELETE and TRUNCATE.
- The outbox still carries notifications, metrics and webhooks.

## Consequences
- No audit entry can be lost or delayed, and none can exist for a change that rolled back.
- Retention and archival (FR-AUD-003) will need a deliberate path around the trigger, such as moving partitions, not row deletes.
- Every new audited action must call `IAuditLog`; `AuditActions` lists the names in use.
