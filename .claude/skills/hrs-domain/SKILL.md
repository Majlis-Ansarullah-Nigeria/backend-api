---
name: hrs-domain
description: Business rules of the reporting domain — unit hierarchy, roles, report routing, observer visibility, report statuses, submission windows, aggregation and reference numbers. Use whenever code touches units, roles, reports, templates, windows or permissions.
user-invocable: false
---

# HRS domain rules

## Hierarchy
- Levels: NATIONAL → ZONE → DILA → MUQAM. DEPARTMENT units hang off NATIONAL. Level names and plurals are configurable data (Dila/Dilaat, Muqam/Muqamaat); never hard-code them in logic.
- `Unit.Path` is a materialized path (`nat.z03.d12.m04`). "Everything under Zone 3" = path prefix query.
- Unit code format example: `Z03-D12-M04`.

## Roles (RoleAssignment = User × Unit × Role × [startsAt, endsAt))
UNIT_HEAD, DELEGATE (scope DraftOnly | DraftAndSubmit, max 2 per unit), SECRETARIAT, LEADERSHIP, DEPT_OFFICER, SYS_ADMIN.
- A user can hold several assignments; every action records which assignment it used (FR-AC-002).
- Access ends at `endsAt`; the next head sees the unit's full history (FR-AC-003).

## Routing (stored per ReportType as data)
| Submitter | Recipient (can act) | Observers (read-only) |
| --- | --- | --- |
| Muqam Head | National Secretariat | parent Dila Head |
| Dila Head | National Secretariat | parent Zone Head |
| Zone Head | National Secretariat | none |
| Department Officer | National Secretariat | Leadership |
- No Dila approval step for Muqam reports in v1.
- Observers: read report + status, download PDF, private notes, nudge (once per 24 h per unit). Never acknowledge/return/edit. Never see Secretariat review comments (FR-AC-005). Confidential fields hidden (FR-AC-006).
- DEPT_OFFICER: read-only access to sections owned by their department in every unit's report at every level (FR-REV-005).

## Report lifecycle
States: NotStarted → Draft → Submitted → InReview → Acknowledged (final) | Returned → (resubmit) Submitted.
- Withdraw: Submitted → Draft, only while unread by the Secretariat and the window is open.
- Missed: hard close passed with no submission; only a Secretariat reopen leaves it.
- `IsLate` is a flag, computed against the (possibly extended) due date. "Overdue" is a derived label for NotStarted/Draft after due.
- Acknowledged can be reopened only by the Secretariat, audited.
- Only the submitter side and the Secretariat change state. Observers never.

## Windows (BR-WIN-01..08)
- Fields: opensAt, dueAt per level (set by the Secretariat on every window — no system default), graceUntil, closesAt. Warn when a parent level is due before its children.
- Before open: preview only. Open→due: on time. Due→grace end: allowed, late. After close: locked → Missed.
- Extension overrides due/close for one unit. Returned reports may be resubmitted after close until the revision deadline (default 7 days).
- Server clock is authoritative; outside the window return `WINDOW_CLOSED`.
- Window jobs (open, remind, close, mark missed) are idempotent; re-running never duplicates notifications.

## Templates
- ReportType → TemplateVersion (draft → published → retired). Published versions are immutable.
- Field: key (stable), type, label, required, min/max, regex, default, unit label, confidential, owningDepartmentId, visibleForLevels, showIf, aggregation (sum|avg|min|max|latest|none), computed expression.
- "Pull from children" fills a parent draft from SUBMITTED child reports using each field's aggregation; it lists included and missing children; heads may override with a note (FR-SUB-007).

## Reference numbers
`{TYPECODE}-{YYYY}-{MM}-{UNITCODE without dashes}`, e.g. `GMR-2026-09-Z03D12M04`. Quarterly: `QTR-2026-Q3-...`. Unique per report instance.

## Out of scope in v1
SMS/WhatsApp, report signatures, native apps, multi-tenancy, languages other than English.
