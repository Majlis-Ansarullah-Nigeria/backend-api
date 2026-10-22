# Implementation plan

Work top to bottom. Each line is sized for one Claude Code session: run `/implement-requirement <id>` and tick the box when `verify` is green and the change is merged. Requirement IDs refer to `SRS.md`.

## M0 — Foundation
- [x] M0.1 Backend solution: `HRS.sln` with Domain, Application, Infrastructure, Api, Worker and the four test projects; `Directory.Build.props` (net10.0, nullable, warnings as errors, analyzers); `global.json`; central package management (`Directory.Packages.props`).
- [x] M0.2 Architecture tests (NetArchTest) for the dependency rule; `Result`/`Error`; `IClock`; ProblemDetails mapping; Serilog; OpenAPI + Scalar UI; `/api/health` (DB, Redis).
- [x] M0.3 EF Core + Npgsql + snake_case naming + first empty migration; Testcontainers-based `ApiFactory` for integration tests.
- [x] M0.4 Frontend scaffold: create the Next.js app in a temp folder and move it into `frontend/` (keep the existing `CLAUDE.md` and `Dockerfile`); Tailwind, shadcn/ui, tokens from `docs/design/README.md`, next-intl, Vitest, Playwright, orval config, `output: 'standalone'`, dev rewrite of `/api/*` to :8080.
- [x] M0.5 App shell from the mockups: sidebar (desktop), bottom tabs (mobile), unit switcher, `StatusChip`, `SaveIndicator`, `EmptyState`, `ErrorState`, `ReadOnlyBanner`.
- [x] M0.6 Docker: confirm `docker compose up` dev stack, build all images, CI green on a PR.

## M1 — Identity and access (FR-IAM, FR-AC, NFR-SEC)
- [x] M1.1 Users + ASP.NET Core Identity, password login, lockout, httpOnly cookie session with refresh rotation (FR-IAM-001, FR-IAM-005, NFR-SEC-02).
- [x] M1.2 Email one-time code login (FR-IAM-001) with rate limits.
- [x] M1.3 Invitations and set-password flow (FR-IAM-002) + Invite screen.
- [x] M1.4 Role assignments with term dates, active assignment switching, `IAccessPolicy` skeleton (FR-AC-001..003).
- [x] M1.5 TOTP MFA required for Secretariat/Admin (FR-IAM-003); sessions list + sign out others (FR-IAM-006).
- [x] M1.6 Login, code, reset, MFA screens per mockups.

## M2 — Organization (FR-ORG)
- [x] M2.1 Unit levels, units with materialized path, CRUD + archive (FR-ORG-001).
- [x] M2.2 Organization tree API + screen with vacant-head badges (FR-ORG-002).
- [x] M2.3 Bulk import: template download, dry run, row errors, idempotent upsert (FR-ORG-003) + wizard screen.
- [x] M2.4 Assign heads, delegates (FR-ORG-004, FR-AC-004); users list (FR-ADM-002).
- [x] M2.5 Move unit to a new parent keeping history (FR-ORG-005).

## M3 — Report types and templates (FR-TPL)
- [x] M3.1 Report types with routing rule data (FR-TPL-001).
- [x] M3.2 Template schema model + JSON validation + versioning draft/publish/immutable (FR-TPL-007, FR-TPL-008).
- [x] M3.3 Shared expression language: C# + TS evaluators + `shared/expression-fixtures.json` parity tests (FR-TPL-005).
- [x] M3.4 Frontend form engine: all field types, conditional logic, computed fields, per-level visibility (FR-TPL-002..004).
- [x] M3.5 Template builder screen (palette, canvas, properties, logic, versions) + preview by level (FR-TPL-009).

## M4 — Windows and schedules (FR-WIN, BR-WIN)
- [x] M4.1 Submission windows with per-level due dates set by the Secretariat, grace, close; parent-before-child warning (FR-WIN-001, FR-WIN-003).
- [x] M4.2 Schedule rules generating windows 12 months ahead (FR-WIN-002); ad-hoc requests (FR-WIN-004).
- [x] M4.3 Extensions and reopen (FR-WIN-005, FR-WIN-006).
- [x] M4.4 Hangfire worker: open, remind, close, mark missed — idempotent (BR-WIN-04, BR-WIN-08).
- [x] M4.5 Windows calendar + create/edit window screens.

## M5 — Submission (FR-SUB)
- [x] M5.1 Report instance lifecycle in the domain with full unit tests (section 5 of SRS).
- [x] M5.2 My reports inbox + head home (FR-SUB-001).
- [x] M5.3 Report form with autosave, `If-Match`, offline queue (FR-SUB-002..004, NFR-RES-01).
- [x] M5.4 Attachments: presigned upload, ClamAV scan, limits (FR-SUB-005).
- [x] M5.5 Review-and-submit, declaration, idempotent submit, reference number, receipt PDF by email (FR-SUB-006, FR-SUB-010).
- [x] M5.6 Withdraw (FR-SUB-009); copy from last period (FR-SUB-008); nil report (FR-SUB-012).

## M6 — Observer views (FR-OBS)
- [x] M6.1 Compliance grid for Dila/Zone heads (FR-OBS-001).
- [x] M6.2 Read-only child report + PDF; confidential fields hidden (FR-OBS-002, FR-AC-005, FR-AC-006).
- [x] M6.3 Pull from children with included/missing list and overrides (FR-SUB-007, FR-OBS-005).
- [x] M6.4 Nudge and private notes (FR-OBS-003, FR-OBS-004).

## M7 — Review at National (FR-REV)
- [x] M7.1 Secretariat inbox with filters and saved views (FR-REV-001).
- [x] M7.2 Mark read, acknowledge, return with comment, flag (FR-REV-002); returned-report screen for heads.
- [x] M7.3 Field-anchored comment threads (FR-REV-003); assignment to staff (FR-REV-004).
- [x] M7.4 Department officer access to their sections across all units (FR-REV-005).

## M8 — Notifications (FR-NOT)
- [x] M8.1 Outbox dispatcher, email templates editable by the Secretariat (FR-NOT-002, FR-NOT-003).
- [x] M8.2 In-app notification centre + SignalR badge; web push (FR-NOT-001).
- [x] M8.3 Preferences, digest, escalation, announcements (FR-NOT-004..006).

## M9 — History and exports (FR-HIS)
- [x] M9.1 History with filters and full-text search, scoped (FR-HIS-001, FR-HIS-002).
- [x] M9.2 Branded PDF + Excel per report (FR-HIS-003); bulk ZIP/Excel exports in background (FR-HIS-004).
- [x] M9.3 Period comparison and version history diff (FR-HIS-005, FR-HIS-006).

## M10 — Dashboards (FR-DSH)
- [x] M10.1 Compliance dashboard with one-click reminders (FR-DSH-001).
- [x] M10.2 Indicator dashboards, drill-down, scoped views (FR-DSH-002, FR-DSH-003, FR-DSH-005); field-level targets (FR-TPL-011); Leadership overview.
- [x] M10.3 Map view (FR-DSH-004); compiled national report export (FR-REV-006).

## M11 — Admin and audit (FR-AUD, FR-ADM)
- [ ] M11.1 Audit log write path for all listed actions + viewer + CSV (FR-AUD-001, FR-AUD-002).
- [ ] M11.2 Organization settings and branding; system health page (FR-ADM-001, FR-ADM-005).
- [ ] M11.3 Webhooks, feature flags (FR-ADM-003, FR-ADM-004); global search palette (FR-SRC-001, FR-SRC-002).

## M12 — Launch readiness
- [ ] M12.1 k6 deadline-spike test meets NFR-PRF-01..03.
- [ ] M12.2 OWASP ZAP baseline in CI, dependency scanning, fix findings; external pen test booked.
- [ ] M12.3 Accessibility pass (axe + screen reader) on all head-facing screens.
- [ ] M12.4 Backups verified by a restore drill (NFR-AVL-02); monitoring and alerts live.
- [ ] M12.5 Staging dress rehearsal with the Secretariat; bulk import of all units; invitations sent.
