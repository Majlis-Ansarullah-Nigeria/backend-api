# Hierarchical Reporting Service — Software Requirements Specification

Oct 4, 2026 · @femi

## 1. Introduction

The Hierarchical Reporting Service (HRS) lets every unit head in the organization — Muqam, Dila, Zone and National department — submit structured reports to the National level on a schedule the National Secretariat controls. Version 1 is a responsive web app built with Next.js (frontend) and ASP.NET Core on .NET 8 LTS or later (backend).

### 1.1 Purpose

This SRS defines what HRS must do, how well it must do it, and how it is built. It is the contract between product, design, engineering, QA and the organization's administrators. Every requirement carries an ID (for example FR-ORG-003) so it can be traced to design, code and test cases.

### 1.2 Scope

In scope for v1:

- Setup of the full hierarchy (National → Zones → Dilaat → Muqamaat) and of National departments, with a named head for each unit.
- Account provisioning so heads can log in, submit, and view their history.
- A template builder for the General Monthly Report and for any other report type the Secretariat defines.
- Submission windows, reminders, late-submission and extension rules.
- Read-only visibility of subordinate reports for parent-unit heads (Dila sees its Muqamaat; Zone sees its Dilaat).
- Review, acknowledgement and revision requests at National level.
- History, search, export (PDF, Excel, CSV) and dashboards on submission compliance and aggregated figures.
- Notifications (in-app, email, web push), audit trail and administration.

Out of scope for v1, planned later: native mobile apps, SMS and WhatsApp notifications, report signatures, offline-first sync, multi-organization tenancy, public reporting portals.

### 1.3 Definitions

| Term | Meaning |
| --- | --- |
| National | The top level of the organization. Owns the Secretariat and the departments. |
| National Secretariat | The office that configures report types, templates and windows, and receives department reports. |
| National Department | A functional office at National (for example Finance, Education, Welfare), led by an Officer. |
| Zone | A regional unit directly under National. Led by a Zone Head. |
| Dila (plural Dilaat) | A district unit inside a Zone. Led by a Dila Head. |
| Muqam (plural Muqamaat) | A local unit inside a Dila. Led by a Muqam Head. |
| Unit | Any node in the hierarchy: National, Zone, Dila, Muqam or Department. |
| Head | The user appointed to lead a unit and submit its reports. |
| Report Type | A kind of report, such as General Monthly Report or an ad-hoc survey. |
| Template | The versioned set of sections and fields a Report Type asks for. |
| Reporting Period | The time a report covers (for example September 2026). |
| Submission Window | The open and close date-times during which a report for a period can be submitted. |
| Report Instance | One unit's report for one Report Type and one period. |
| Recipient | The unit that receives and acts on a report. In v1 this is National (or the Secretariat for department reports). |
| Observer | A unit that can read a report but not act on it (the parent Dila or Zone). |

### 1.4 Conventions

Requirement keywords follow RFC 2119: **MUST** is mandatory for v1, **SHOULD** is expected unless a reason is recorded, **MAY** is optional. Priority tags: P1 (launch blocker), P2 (launch target), P3 (later release).

### 1.5 References

Next.js App Router docs, ASP.NET Core docs, OWASP ASVS 4.0 Level 2, WCAG 2.2 AA, and the organization's constitution or bylaws describing unit structure and offices.

## 2. Overall description

HRS replaces paper, email and WhatsApp reporting with one system where every report goes to National, and parent units watch their children's reports without acting on them.

### 2.1 Organization model

The hierarchy is a tree of four levels plus a set of National departments. Every report flows upward to National; the Dila and Zone receive a read-only copy for awareness and for building their own report.

&#91;embedded content: organization hierarchy and report routing · 4 levels\]

Solid lines carry reports that the Secretariat can act on; dashed lines give the parent head a read-only copy.

The tree MUST be modelled generically (a Unit table with a parent and a level), so a fifth level, a renamed level, or units moving between parents need configuration, not code.

### 2.2 User classes and personas

| Persona | Typical user | Main goals | Tech comfort |
| --- | --- | --- | --- |
| Muqam Head | Volunteer leading a local unit, often on a phone | Fill the monthly report fast, see what is due, download past reports | Low to medium |
| Dila Head | Leads several Muqamaat | Submit own report, see which Muqamaat have reported, reuse their figures | Medium |
| Zone Head | Leads several Dilaat | Submit own report, monitor Dilaat compliance | Medium |
| National Department Officer | Head of a National department | Submit department report to the Secretariat, view national figures in their area | Medium to high |
| National Secretariat staff | Secretary and assistants | Build templates, open windows, chase late units, review and compile | High |
| National Leadership (read-only) | President or executive council | Dashboards and compiled national reports | Medium |
| System Administrator | IT volunteer or staff | Set up units and users, roles, integrations, backups | High |
| Deputy or Delegate | Assistant appointed by a head | Draft or submit on the head's behalf, with attribution | Low to medium |

### 2.3 Operating environment

- Clients: current Chrome, Edge, Safari and Firefox on desktop and mobile; screens from 360 px wide. Installable as a Progressive Web App.
- Network: MUST stay usable on 3G-class mobile connections (initial page under 200 KB of JavaScript on key screens).
- Server: Linux containers running ASP.NET Core; PostgreSQL; Redis; S3-compatible object storage.

### 2.4 Constraints

- Frontend in Next.js (App Router, TypeScript). Backend in C# on ASP.NET Core.
- Personal data handling MUST comply with the data protection law of the country where the organization operates (for example Nigeria's NDPA 2023 if based there). All data is hosted in a public cloud, in a region chosen to satisfy that law.
- Heads change at elections or appointments; the system MUST keep reports tied to the unit, not the person.

### 2.5 Assumptions and dependencies

- Every head has an email address; login, OTP and notifications are email-based in v1.
- An email provider (for example SendGrid, Amazon SES or SMTP) is available.
- The Secretariat supplies the initial list of zones, dilaat, muqamaat, departments and heads as a spreadsheet for bulk import.

## 3. Roles, permissions and visibility

A report has exactly one submitting unit, one recipient that can act on it, and zero or more observers that can only read it. Access is decided by the user's role assignment on a unit plus the report's routing rule, never by hard-coded level checks.

### 3.1 Report routing (v1 defaults)

| Submitter | Recipient (can act) | Observers (read-only) |
| --- | --- | --- |
| Muqam Head | National Secretariat | Parent Dila Head |
| Dila Head | National Secretariat | Parent Zone Head |
| Zone Head | National Secretariat | None |
| National Department Officer | National Secretariat | National Leadership |

Routing MUST be stored per Report Type as data (recipient level, observer depth), so routing can change later without a release. Decided for v1: Muqam reports need no Dila approval. Observer depth SHOULD be configurable (for example, a Zone Head may also see Muqam reports).

### 3.2 Role-based permissions

| Capability | Muqam Head | Dila Head | Zone Head | Dept. Officer | Secretariat | Leadership | Sys Admin |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Submit own unit's reports | Yes | Yes | Yes | Yes | Yes (own) | No | No |
| View own unit's history and download | Yes | Yes | Yes | Yes | Yes | Yes | No |
| View child units' reports | No | Muqamaat | Dilaat | Own department's sections, all units | All | All | No |
| Import child figures into own draft | No | Yes | Yes | No | Yes | No | No |
| Comment on child reports | No | Optional (private notes) | Optional (private notes) | No | Yes | No | No |
| Acknowledge or return a report | No | No | No | No | Yes | No | No |
| Build and publish templates | No | No | No | No | Yes | No | No |
| Open, close, extend windows | No | No | No | No | Yes | No | No |
| Grant extension to one unit | No | No | No | No | Yes | No | No |
| National dashboards and compiled report | No | Own Dila scope | Own Zone scope | Own department | Yes | Yes | No |
| Manage units and heads | No | No | No | No | Yes | No | Yes |
| Manage system settings, integrations | No | No | No | No | No | No | Yes |
| View audit log | No | No | No | No | Yes | No | Yes |

### 3.3 Rules

- **FR-AC-001 (P1)** Permissions MUST be evaluated server-side with ASP.NET Core policy-based authorization; the UI only hides what the API already forbids.
- **FR-AC-002 (P1)** A user MAY hold several role assignments (for example a Dila Head who is also a Muqam Head). The UI MUST provide a unit switcher, and each action MUST record which assignment it used.
- **FR-AC-003 (P1)** Role assignments have start and end dates. When a head is replaced, the outgoing head loses access at the end date and the incoming head sees the unit's full history.
- **FR-AC-004 (P2)** A head MAY appoint up to two delegates per unit, scoped to draft-only or draft-and-submit. Submissions show "Submitted by X on behalf of Y".
- **FR-AC-005 (P1)** Observers MUST NOT see the recipient's internal review comments, only the report and its status.
- **FR-AC-006 (P2)** Field-level confidentiality: a template field marked confidential is hidden from observers (for example a welfare case with names).

## 4. Functional requirements

Fourteen modules make up v1; organization setup, identity, templates, windows and submission are the P1 core without which nothing else works.

### 4.1 Organization setup (ORG)

- **FR-ORG-001 (P1)** Admins can create, rename, archive and restore Zones, Dilaat, Muqamaat and National Departments, each with a code (for example `Z03-D12-M04`), name, parent, address, GPS location (optional), contact phone and email.
- **FR-ORG-002 (P1)** Tree view of the full hierarchy with search, expand/collapse, unit counts per level, and a "vacant head" badge.
- **FR-ORG-003 (P1)** Bulk import from Excel/CSV with a downloadable template, a dry-run preview, row-level validation errors, and idempotent re-import (matched by unit code).
- **FR-ORG-004 (P1)** Assign a head to any unit by selecting an existing user or creating one inline; set term start and end dates.
- **FR-ORG-005 (P2)** Move a unit to a new parent (for example a Muqam moves to another Dila). History keeps the old parent for reports submitted before the move.
- **FR-ORG-006 (P2)** Merge and split units, with a wizard that maps history to the surviving unit.
- **FR-ORG-007 (P2)** Configurable level names and plural labels (Dila/Dilaat, Muqam/Muqamaat) and an optional fifth level.
- **FR-ORG-008 (P3)** Unit profile page: head, deputies, membership size fields, map pin, report compliance history.

### 4.2 Identity and access (IAM)

- **FR-IAM-001 (P1)** Login with email or phone number plus password, or passwordless one-time code (email OTP in v1; SMS and WhatsApp deferred).
- **FR-IAM-002 (P1)** Invitation flow: when a head is assigned, the system sends an invite link valid for 7 days; the user sets a password and verifies contact details.
- **FR-IAM-003 (P1)** Mandatory TOTP or passkey multi-factor authentication for Secretariat and Admin roles; optional for others.
- **FR-IAM-004 (P2)** Passkeys (WebAuthn) as a first-class login option.
- **FR-IAM-005 (P1)** Self-service password reset; account lockout after 5 failed attempts for 15 minutes.
- **FR-IAM-006 (P1)** Session list with "sign out other devices"; admins can force sign-out and deactivate users.
- **FR-IAM-007 (P2)** Handover: when a term ends, the system prompts the incoming head to accept the role and shows a handover summary (open reports, pending revisions, deadlines).
- **FR-IAM-008 (P3)** Single sign-on through Google or Microsoft accounts for orgs that use them.

### 4.3 Report types and template builder (TPL)

- **FR-TPL-001 (P1)** The Secretariat creates Report Types with: name, description, category (General Monthly, Quarterly, Annual, Ad-hoc, Event, Survey), applicable levels (which units must submit), frequency, routing rule (section 3.1) and whether attachments are allowed.
- **FR-TPL-002 (P1)** Drag-and-drop template builder with sections and fields. Field types: short text, long text (rich text), number, currency, percentage, date, date range, yes/no, single choice, multiple choice, dropdown, rating, file/image upload, repeatable table (rows of sub-fields), member list, location, and computed field.
- **FR-TPL-003 (P1)** Per-field settings: label, help text, placeholder, required, min/max, regex, default value, unit (for example "members"), confidential flag, owning National department (drives FR-REV-005 access), and per-level visibility (a field shown only to Muqam reports).
- **FR-TPL-004 (P1)** Conditional logic: show, hide or require a field or section based on other answers (for example "If 'Held general meeting?' = Yes, ask attendance").
- **FR-TPL-005 (P1)** Computed fields with a safe expression language (sum, average, percentage, if) referencing other fields, evaluated identically on client and server.
- **FR-TPL-006 (P1)** Aggregation rule per numeric field: sum, average, min, max, latest, or none. This drives roll-ups from Muqam to Dila to Zone to National and the "import child figures" feature (FR-SUB-007).
- **FR-TPL-007 (P1)** Versioning: templates are drafted, previewed, then published as an immutable version. Each Report Instance stores the version it was filled against; old reports always render with their original version.
- **FR-TPL-008 (P1)** Editing a published template creates a new draft version; publishing it applies to windows opened after that point. The Secretariat MAY choose to migrate unsubmitted drafts, with a field mapping preview.
- **FR-TPL-009 (P2)** Live preview as each level would see it, on desktop and mobile frames.
- **FR-TPL-010 (P2)** Template library: duplicate, archive, import/export as JSON, and starter templates.
- **FR-TPL-011 (P2)** Field-level targets (for example "Target: 20 new members") so dashboards can show target versus actual.
- **FR-TPL-012 (P3)** Multi-language labels for every field, chosen by the user's language preference.

### 4.4 Reporting periods and submission windows (WIN)

- **FR-WIN-001 (P1)** For each Report Type and period, the Secretariat sets the window: open date-time, due date-time, grace period (late but accepted) and hard close. All times stored in UTC and shown in the organization's time zone (default Africa/Lagos, configurable).
- **FR-WIN-002 (P1)** Recurring schedules: a rule such as "General Monthly Report for month M opens on day 1 of M+1 and is due on day 10" generates windows automatically 12 months ahead; each generated window can still be edited.
- **FR-WIN-003 (P1)** Different due dates per level are allowed (for example Muqam due day 5, Dila due day 10, Zone due day 15) so parents can include child figures.
- **FR-WIN-004 (P1)** Ad-hoc requests: the Secretariat issues a one-off Report Type to all units, one level, or a hand-picked list of units, with its own window.
- **FR-WIN-005 (P1)** Extensions: grant a new due date to one unit or a group, with a reason; the unit is notified.
- **FR-WIN-006 (P2)** Reopen a closed window for selected units to allow late submission, flagged as late.
- **FR-WIN-007 (P2)** Calendar view of all windows across report types, with conflicts highlighted.
- **FR-WIN-008 (P2)** Blackout and holiday calendar that shifts due dates falling on configured holidays.

### 4.5 Report submission (SUB)

- **FR-SUB-001 (P1)** "My reports" inbox lists every report due for the user's unit(s): type, period, status, due date, and a countdown; sorted by urgency.
- **FR-SUB-002 (P1)** Form filling renders the template version for that window, with inline validation, conditional logic and computed fields.
- **FR-SUB-003 (P1)** Autosave every 10 seconds and on blur; drafts survive closing the browser and switching devices. A "last saved" indicator is always visible.
- **FR-SUB-004 (P1)** Section-by-section stepper on mobile with progress percentage; single long form with a sticky section index on desktop.
- **FR-SUB-005 (P1)** Attachments: images, PDF, Office files up to 20 MB each and 100 MB per report, virus-scanned before acceptance; images compressed client-side.
- **FR-SUB-006 (P1)** Pre-submit review screen showing all answers, missing required fields, and warnings (for example a figure 50% below last month). Submission requires an explicit confirmation declaration.
- **FR-SUB-007 (P1)** Import child figures: a Dila Head (or Zone Head) clicks "Pull from Muqamaat" and the draft fills numeric fields using each field's aggregation rule over submitted child reports. The panel shows which children are included, which are missing, and lets the head override any value with a note.
- **FR-SUB-008 (P2)** Copy from last period: prefill text and settings fields from the unit's previous report of the same type.
- **FR-SUB-009 (P1)** Once submitted, the report is locked. The head MAY withdraw it while it is still unread by the recipient and the window is open; otherwise changes go through a revision request.
- **FR-SUB-010 (P1)** Submission receipt: a reference number (for example `GMR-2026-09-Z03D12M04`), timestamp, and a PDF copy emailed to the head.
- **FR-SUB-011 (P2)** Collaborative drafting: delegates and the head can edit the same draft; field-level last-editor shown; optimistic concurrency prevents silent overwrites.
- **FR-SUB-012 (P2)** Nil report: a one-click "No activity this period" submission where the template allows it.
- **FR-SUB-013 (P3)** Voice-to-text dictation on long-text fields using the browser's speech API.

### 4.6 Observer view for parent units (OBS)

- **FR-OBS-001 (P1)** Dila Heads see a compliance grid of their Muqamaat per report type and period: Not started, Draft, Submitted (on time), Submitted late, Overdue.
- **FR-OBS-002 (P1)** Observers open any submitted child report read-only, download it as PDF, and see its status. No approve, return or edit actions appear.
- **FR-OBS-003 (P2)** Observers can send a nudge (reminder) to an overdue child unit, rate-limited to once per 24 hours per unit.
- **FR-OBS-004 (P2)** Observers can keep private notes on a child report, visible only to themselves and their successors in office.
- **FR-OBS-005 (P2)** Roll-up preview: the aggregated totals of all child reports, side by side with the head's own draft.

### 4.7 Review at National (REV)

- **FR-REV-001 (P1)** The Secretariat inbox lists incoming reports with filters (type, period, level, zone, status, late, flagged) and saved views.
- **FR-REV-002 (P1)** Actions on a report: mark as read, acknowledge, return for revision with a required comment, and flag for follow-up. Each action notifies the submitter.
- **FR-REV-003 (P1)** Inline comments anchored to a field; threaded replies with the submitter.
- **FR-REV-004 (P2)** Assign reports to Secretariat staff members (for example by zone) with a workload view.
- **FR-REV-005 (P1)** Department reports route to the Secretariat; each department officer MUST have read-only access to their department's sections in every unit's report, at every level, (for example Finance sees all finance sections).
- **FR-REV-006 (P2)** Compile national report: generate a consolidated document for a period from all received reports, with aggregates, tables and narrative highlights, exportable to Word and PDF.
- **FR-REV-007 (P3)** AI-assisted summary: produce a draft narrative summary of a zone's or the nation's reports for a period, clearly labelled as machine-generated and editable before use.

### 4.8 History, search and export (HIS)

- **FR-HIS-001 (P1)** Every user sees all past reports of every unit they head or observe, including those submitted by previous heads.
- **FR-HIS-002 (P1)** Filters: report type, period range, status, unit; full-text search across text answers within the user's access scope.
- **FR-HIS-003 (P1)** Download a single report as a branded PDF (logo, unit, period, reference number, QR code linking to the online record) or as Excel.
- **FR-HIS-004 (P1)** Bulk export: select many reports and download a ZIP of PDFs, or one Excel workbook with a row per report and a column per field. Large exports run in the background and notify when ready.
- **FR-HIS-005 (P2)** Period-over-period comparison of the same unit, with changed figures highlighted.
- **FR-HIS-006 (P2)** Version history of each report: every submission, withdrawal and revision with a diff view.

### 4.9 Notifications and reminders (NOT)

- **FR-NOT-001 (P1)** Channels in v1: in-app notification centre, email, and web push for installed PWAs. Channels sit behind an adapter interface so SMS or WhatsApp can be added later without changing callers.
- **FR-NOT-002 (P1)** Events: window opened, reminder (configurable, for example 5 days, 2 days and 1 day before due), overdue, extension granted, report acknowledged, returned for revision, comment added, child submitted (to observer), template changed.
- **FR-NOT-003 (P1)** Notification templates editable by the Secretariat with placeholders ({{headName}}, {{unitName}}, {{dueDate}}, {{link}}).
- **FR-NOT-004 (P2)** User preferences per event and channel; daily digest option; quiet hours.
- **FR-NOT-005 (P2)** Escalation: if a Muqam is 3 days overdue, notify its Dila Head; if 7 days, notify its Zone Head (configurable).
- **FR-NOT-006 (P2)** Announcements: the Secretariat broadcasts a message to all heads or a level, shown as a banner and sent by email.

### 4.10 Dashboards and analytics (DSH)

- **FR-DSH-001 (P1)** Compliance dashboard: submission rate per period by zone, dila and level; on-time versus late; a list of overdue units with one-click reminders.
- **FR-DSH-002 (P2)** Indicator dashboards built from numeric template fields: totals, trends over 12 months, ranking of zones/dilaat, target versus actual.
- **FR-DSH-003 (P2)** Drill-down from National to Zone to Dila to Muqam on any chart.
- **FR-DSH-004 (P2)** Map view of units coloured by compliance or an indicator value (where GPS is set).
- **FR-DSH-005 (P2)** Scoped dashboards for Zone and Dila Heads limited to their subtree.
- **FR-DSH-006 (P3)** Scheduled dashboard snapshots emailed to leadership monthly.

### 4.11 Search and command palette (SRC)

- **FR-SRC-001 (P2)** Global search (Ctrl/Cmd+K) across units, users, reports and templates, filtered by permission.
- **FR-SRC-002 (P2)** Quick actions in the palette: "Open September report", "Go to Zone 3", "Create report type".

### 4.12 Audit and compliance (AUD)

- **FR-AUD-001 (P1)** Append-only audit log of logins, role changes, unit changes, template publishes, window changes, submissions, reviews, exports and downloads: who, what, when, IP, user agent, before/after values.
- **FR-AUD-002 (P1)** Audit log viewer with filters and CSV export for Secretariat and Admin.
- **FR-AUD-003 (P2)** Data retention policy per Report Type (for example 7 years), with archival to cold storage and legal hold.

### 4.13 Administration and settings (ADM)

- **FR-ADM-001 (P1)** Organization settings: name, logo, colours (applied to UI accents and PDFs), time zone, date format, fiscal year start.
- **FR-ADM-002 (P1)** User management: search, create, deactivate, reset MFA, view role assignments and login history.
- **FR-ADM-003 (P2)** Integration settings: email provider, storage, webhooks (report.submitted, report.acknowledged) for other systems.
- **FR-ADM-004 (P2)** Feature flags to enable modules per organization stage.
- **FR-ADM-005 (P2)** System health page: job queue status, failed notifications, storage usage.

### 4.14 Help and onboarding (HLP)

- **FR-HLP-001 (P2)** First-login guided tour per role and contextual help text on each template field.
- **FR-HLP-002 (P2)** Help centre with short how-to videos and FAQs, editable by the Secretariat.
- **FR-HLP-003 (P3)** In-app feedback and support request linked to the current screen.

## 5. Report lifecycle and window rules

A Report Instance moves through seven states, and only the submitter or the Secretariat can move it; observers never change its state.

&#91;embedded content: report lifecycle · 7 states\]

Acknowledged is the only final state; a returned report re-enters at Submitted, and a missed one only via a Secretariat reopen.

### 5.1 States

| State | Meaning | Who moves it out |
| --- | --- | --- |
| Not started | Window open, nothing saved yet | Submitter (by saving) |
| Draft | Saved, not submitted | Submitter (submit) |
| Submitted | Sent to National, unread | Submitter (withdraw) or Secretariat (open) |
| In review | Opened by the Secretariat | Secretariat (acknowledge or return) |
| Returned | Sent back with comments | Submitter (resubmit) |
| Acknowledged | Accepted; final | Secretariat (reopen, rare, audited) |
| Missed | Hard close passed with no submission | Secretariat (reopen window for that unit) |

On time or late is a flag on the submission, not a separate state. "Overdue" is a derived label for Not started or Draft after the due date.

### 5.2 Window rules

- **BR-WIN-01** Before open: the report is visible as "Upcoming" with its open date; the form can be previewed but not saved.
- **BR-WIN-02** Open to due: saving and submitting are allowed; submissions are flagged on time.
- **BR-WIN-03** Due to grace end: submitting is allowed and flagged late; reminders switch to overdue wording.
- **BR-WIN-04** After hard close: the form locks; unsubmitted instances become Missed. Drafts are kept for reference.
- **BR-WIN-05** An extension overrides due and close dates for that unit only; the late flag is computed against the extended due date.
- **BR-WIN-06** A returned report can be resubmitted after the hard close until a revision deadline set by the reviewer (default 7 days).
- **BR-WIN-07** The server clock is authoritative; the client shows a countdown but the API rejects submissions outside the window with error `WINDOW_CLOSED`.
- **BR-WIN-08** Window jobs (open, remind, close, mark missed) run idempotently on a scheduler; re-running a job never sends duplicate notifications.

## 6. Non-functional requirements

HRS must be secure enough to hold member data, fast on a phone over 3G, and survive the deadline-day spike when most units submit in the last 24 hours.

| ID | Area | Requirement |
| --- | --- | --- |
| NFR-SEC-01 | Security | Meet OWASP ASVS 4.0 Level 2. TLS 1.2+ everywhere, HSTS, strict Content Security Policy. |
| NFR-SEC-02 | Security | Passwords hashed with ASP.NET Core Identity (PBKDF2, 100k+ iterations) or Argon2id. Access tokens 15 minutes, refresh tokens rotated and stored in httpOnly, Secure, SameSite cookies. |
| NFR-SEC-03 | Security | Encryption at rest for database and object storage; confidential fields additionally encrypted at column level. |
| NFR-SEC-04 | Security | Rate limiting on auth and submission endpoints; CSRF protection; uploads virus-scanned (ClamAV) and served via short-lived signed URLs. |
| NFR-SEC-05 | Security | Secrets in a vault (Azure Key Vault, AWS Secrets Manager or Doppler), never in source. Dependency and container scanning in CI. |
| NFR-PRF-01 | Performance | API p95 under 300 ms for reads and 600 ms for submissions at peak load. |
| NFR-PRF-02 | Performance | Largest Contentful Paint under 2.5 s on a mid-range Android phone over 4G; key screens usable over 3G. |
| NFR-PRF-03 | Performance | Peak load: all units submitting within 2 hours of a deadline (for example 5,000 units, 50 submissions per second burst) without errors. |
| NFR-PRF-04 | Performance | Exports of up to 5,000 reports complete within 5 minutes in the background. |
| NFR-AVL-01 | Availability | 99.5% monthly uptime; 99.9% in the 72 hours before a due date. |
| NFR-AVL-02 | Recovery | Recovery point objective 15 minutes (point-in-time database recovery); recovery time objective 4 hours. Daily backups kept 35 days, monthly kept 12 months, restore drill quarterly. |
| NFR-SCL-01 | Scalability | Stateless API nodes scale horizontally; background jobs scale independently. |
| NFR-ACC-01 | Accessibility | WCAG 2.2 AA: keyboard navigation, screen-reader labels, 4.5:1 contrast, focus states, no colour-only status. |
| NFR-I18N-01 | Localization | All UI text externalized; English at launch, structure ready for other languages and right-to-left scripts. |
| NFR-PRV-01 | Privacy | Data minimization; consent and privacy notice at first login; data export and deletion requests handled within 30 days; data processing records kept. |
| NFR-OBS-01 | Observability | Structured logs (Serilog), metrics and traces (OpenTelemetry) with correlation IDs from browser to database; alerting on error rate, latency and failed jobs. |
| NFR-MNT-01 | Maintainability | 80% unit-test coverage on domain and application layers; OpenAPI spec generated from code; architecture decision records kept in the repo. |
| NFR-USE-01 | Usability | A first-time Muqam Head completes a monthly report without help in under 15 minutes in usability testing (5 of 6 participants). |
| NFR-RES-01 | Resilience | Draft autosave survives a network drop: unsaved changes are queued in the browser (IndexedDB) and synced on reconnect. |

## 7. System architecture

A Next.js web app talks to a single ASP.NET Core modular monolith over REST; background workers handle windows, notifications and exports. A modular monolith keeps v1 simple to run while module boundaries allow later extraction.

&#91;embedded content: system architecture · web, API, workers, data\]

The API raises domain events into an outbox; workers pick them up to send notifications, run window jobs and build exports, so a slow email provider never slows a submission.

### 7.1 Technology stack

| Layer | Choice | Notes |
| --- | --- | --- |
| Frontend framework | Next.js 15+ (App Router), React, TypeScript | Server Components for read-heavy pages; client components for forms |
| UI kit | Tailwind CSS + shadcn/ui (Radix primitives) | Accessible components, themeable with org colours |
| Forms | React Hook Form + Zod, generated from template JSON | Same validation schema mirrored on the server |
| Data fetching | TanStack Query, typed client generated from OpenAPI (orval or NSwag) | Caching, optimistic updates, retries |
| Charts | Recharts or Apache ECharts | Dashboards and drill-downs |
| Template builder | dnd-kit for drag and drop | Sections, fields, reorder |
| Auth in frontend | Backend-issued tokens in httpOnly cookies; Next.js middleware for route guards | Backend remains the authority |
| Backend | ASP.NET Core (.NET 8 LTS or later), C# | Minimal APIs or controllers, Clean Architecture layers |
| Patterns | MediatR (CQRS), FluentValidation, Mapster | One handler per use case |
| ORM | Entity Framework Core + Npgsql | Migrations in CI |
| Identity | ASP.NET Core Identity + OpenIddict (or Duende IdentityServer) | OTP, TOTP, passkeys |
| Database | PostgreSQL 16 | JSONB for report answers, ltree or materialized path for the hierarchy |
| Cache and locks | Redis | Sessions, rate limits, distributed locks for jobs |
| Background jobs | Hangfire (PostgreSQL storage) or Quartz.NET | Window open/close, reminders, exports, PDF |
| Real-time | SignalR | Live notification badge, collaborative draft presence |
| Files | S3-compatible storage (AWS S3, Azure Blob, MinIO) | Signed URLs, ClamAV scan on upload |
| PDF and Excel | QuestPDF, ClosedXML | Branded report PDFs, bulk workbooks |
| Search | PostgreSQL full-text search (v1); OpenSearch later if needed | Scoped by permissions |
| Email | Email adapter (Amazon SES or SendGrid); SMS/WhatsApp deferred | Outbox pattern for reliable delivery |
| Observability | Serilog, OpenTelemetry, Grafana or Azure Monitor, Sentry | Traces from browser to database |
| Deployment | Docker; Azure App Service or Container Apps, or AWS ECS; Vercel or containerized Next.js | Infrastructure as code (Terraform or Bicep) |
| CI/CD | GitHub Actions | Build, test, scan, migrate, deploy to staging then production |

### 7.2 Backend modules

Organization, Identity, Templates, Windows, Submissions, Review, Notifications, Analytics, Exports, Audit and Admin. Each module owns its tables and exposes an application service; modules communicate through in-process domain events (for example `ReportSubmitted`), persisted via an outbox so notifications and audit entries are never lost.

### 7.3 Key design decisions

- **Template as JSON schema.** A published template version is stored as a JSON document; answers are stored as JSONB keyed by stable field IDs. Numeric fields used in dashboards are also written to a narrow `report_metric` table for fast aggregation.
- **Hierarchy queries.** A materialized path (for example `nat.z03.d12.m04`) or PostgreSQL ltree makes "all reports in Zone 3" a single indexed query.
- **Authorization.** Resource-based policies check the user's active role assignment and the report's routing; row-level filters are applied in every query through a shared specification.
- **Idempotency.** Submit and other state-changing calls accept an `Idempotency-Key` header so retries on poor networks never double-submit.
- **Environments.** Development, staging (with anonymized data) and production, each with its own database and storage.

## 8. Data model

Eighteen core entities cover the product; reports point to a unit and a template version, never to a person, so history survives changes of head.

| Entity | Key fields | Relationships |
| --- | --- | --- |
| Organization | id, name, logo, timezone, settings (JSONB) | Has many Units, Users |
| UnitLevel | id, code (NATIONAL, ZONE, DILA, MUQAM, DEPARTMENT), name, plural\_name, depth | Has many Units |
| Unit | id, code, name, level\_id, parent\_id, path (ltree), status, contact, geo, archived\_at | Self-referencing tree; has RoleAssignments, ReportInstances |
| User | id, name, email, phone, password\_hash, mfa, language, status, last\_login\_at | Has RoleAssignments, Sessions |
| Role | id, code (UNIT\_HEAD, DELEGATE, SECRETARIAT, LEADERSHIP, DEPT\_OFFICER, SYS\_ADMIN), permissions | Used by RoleAssignments |
| RoleAssignment | id, user\_id, unit\_id, role\_id, starts\_at, ends\_at, scope (draft/submit), appointed\_by | User × Unit × Role over time |
| ReportType | id, name, category, frequency, applicable\_levels\[\], routing\_rule (JSONB), allow\_attachments, status | Has TemplateVersions, Windows |
| TemplateVersion | id, report\_type\_id, version\_no, schema (JSONB), status (draft/published/retired), published\_at, published\_by | Used by Windows and ReportInstances |
| ScheduleRule | id, report\_type\_id, recurrence, open\_offset, due\_offsets per level, grace, close\_offset | Generates Windows |
| SubmissionWindow | id, report\_type\_id, template\_version\_id, period\_start, period\_end, opens\_at, due\_at, grace\_until, closes\_at, target\_scope | Has ReportInstances, Extensions |
| Extension | id, window\_id, unit\_id, new\_due\_at, new\_close\_at, reason, granted\_by | One unit's override |
| ReportInstance | id, window\_id, unit\_id, template\_version\_id, status, answers (JSONB), is\_late, submitted\_at, submitted\_by, on\_behalf\_of, reference\_no, row\_version | Has Attachments, Comments, Revisions |
| ReportRevision | id, report\_instance\_id, revision\_no, answers snapshot, action, actor\_id, created\_at | Immutable history |
| ReportMetric | report\_instance\_id, field\_key, numeric\_value, unit\_id, unit\_path, period\_start | Denormalized for dashboards |
| Attachment | id, report\_instance\_id, field\_key, file\_name, mime, size, storage\_key, scan\_status | Stored in object storage |
| Comment | id, report\_instance\_id, field\_key, author\_id, body, visibility (thread/private), parent\_id | Review threads and private notes |
| Notification | id, user\_id, event, channel, payload, status, sent\_at, read\_at | Delivery log |
| AuditEvent | id, actor\_id, action, entity, entity\_id, before, after, ip, user\_agent, at | Append-only |

All tables carry `created_at`, `updated_at`, `created_by` and soft-delete where relevant. A unique index on (window\_id, unit\_id) guarantees one report per unit per window.

Example template field in the schema JSON:

```json
{
  "key": "new_members",
  "type": "number",
  "label": "New members this month",
  "required": true,
  "min": 0,
  "aggregation": "sum",
  "visibleForLevels": ["MUQAM", "DILA", "ZONE"],
  "confidential": false,
  "showIf": null
}
```

## 9. API specification outline

The backend exposes a versioned REST API at `/api/v1`, documented by an OpenAPI 3.1 spec generated from code, with errors returned as RFC 9457 Problem Details.

| Resource | Endpoints |
| --- | --- |
| Auth | `POST /auth/login`, `POST /auth/otp/request`, `POST /auth/otp/verify`, `POST /auth/refresh`, `POST /auth/logout`, `POST /auth/password/reset`, `POST /auth/invitations/{token}/accept`, `GET /auth/sessions`, `DELETE /auth/sessions/{id}` |
| Me | `GET /me` (profile + role assignments), `PUT /me/active-assignment`, `GET /me/inbox` (reports due), `GET/PUT /me/notification-preferences` |
| Units | `GET /units/tree`, `GET /units?level=&parentId=&q=`, `POST /units`, `GET/PUT/DELETE /units/{id}`, `POST /units/{id}/move`, `POST /units/import` (dry run via `?dryRun=true`), `GET /units/{id}/compliance?periodFrom=&periodTo=` |
| Users and roles | `GET/POST /users`, `GET/PUT /users/{id}`, `POST /users/{id}/deactivate`, `POST /units/{id}/assignments`, `PUT/DELETE /assignments/{id}`, `POST /assignments/{id}/delegates` |
| Report types | `GET/POST /report-types`, `GET/PUT /report-types/{id}`, `POST /report-types/{id}/archive` |
| Templates | `GET /report-types/{id}/versions`, `POST /report-types/{id}/versions` (new draft), `PUT /template-versions/{id}`, `POST /template-versions/{id}/publish`, `GET /template-versions/{id}/preview?level=` |
| Windows | `GET/POST /windows`, `PUT /windows/{id}`, `POST /windows/{id}/close`, `POST /windows/{id}/reopen`, `POST /windows/{id}/extensions`, `GET/POST /schedule-rules` |
| Reports | `GET /reports?windowId=&unitId=&status=&scope=`, `GET /reports/{id}`, `PATCH /reports/{id}/answers` (autosave, `If-Match` row version), `POST /reports/{id}/submit` (`Idempotency-Key`), `POST /reports/{id}/withdraw`, `GET /reports/{id}/child-rollup`, `GET /reports/{id}/revisions`, `GET /reports/{id}/pdf` |
| Attachments | `POST /reports/{id}/attachments/upload-url`, `POST /reports/{id}/attachments/{aid}/complete`, `GET /attachments/{aid}/download-url`, `DELETE /attachments/{aid}` |
| Review | `POST /reports/{id}/mark-read`, `POST /reports/{id}/acknowledge`, `POST /reports/{id}/return`, `POST /reports/{id}/flag`, `POST /reports/{id}/assign`, `GET/POST /reports/{id}/comments` |
| Observer | `GET /units/{id}/children/reports?windowId=`, `POST /units/{id}/nudge`, `GET/POST /reports/{id}/private-notes` |
| Exports | `POST /exports` (filters + format), `GET /exports/{id}` (status + download URL) |
| Analytics | `GET /analytics/compliance`, `GET /analytics/metrics?field=&groupBy=&from=&to=&unitPath=`, `GET /analytics/national-compilation?windowId=` |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read`, `POST /notifications/read-all`, `GET/PUT /notification-templates`, `POST /announcements` |
| Audit and admin | `GET /audit-events`, `GET/PUT /settings`, `GET /health`, `GET/POST /webhooks` |

Conventions: cursor pagination (`?cursor=&limit=`), filtering by query string, ISO 8601 UTC timestamps, ETag/`If-Match` for optimistic concurrency, `429` with `Retry-After` on rate limits. A SignalR hub at `/hubs/notifications` pushes notification and presence events.

## 10. UI/UX specification

The design goal is that a Muqam Head on a phone sees what is due within one tap of logging in and finishes the monthly report in under 15 minutes.

### 10.1 Design principles

1. **Due first.** Every role lands on what needs their action now, ranked by deadline.
2. **Mobile first, desktop strong.** Forms are designed at 360 px, then expanded; the Secretariat's tables and builders are designed for wide screens.
3. **Never lose work.** Autosave, offline queue and clear "saved" feedback everywhere.
4. **Read-only looks read-only.** Observer views use a distinct banner and no action buttons, so a Dila Head never wonders whether they should approve.
5. **Status without colour alone.** Every status chip has an icon and a word.
6. **Plain language.** Labels use the organization's own terms (Dila, Muqam), configurable per organization.

### 10.2 Navigation by role

| Role | Primary navigation |
| --- | --- |
| Muqam Head | Home (due now), My reports, History, Notifications, Profile |
| Dila / Zone Head | Home, My reports, My units (compliance grid + child reports), History, Dashboard, Notifications |
| Department Officer | Home, My reports, Department data, History, Notifications |
| Secretariat | Overview, Inbox, Report types and templates, Windows calendar, Organization, Users, Dashboards, Exports, Announcements, Audit, Settings |
| Leadership | Overview, Dashboards, Compiled reports |
| Sys Admin | Organization, Users, Integrations, System health, Audit |

Desktop uses a collapsible left sidebar with a unit switcher at the top; mobile uses a bottom tab bar with four items and a "More" sheet.

### 10.3 Key screens

- **Home (heads):** greeting, active unit, "Due now" cards with countdown and progress ring, recent activity, and for Dila/Zone heads a compliance strip of child units.
- **Report form:** sticky header (type, period, due date, save state), section stepper, field help in a side drawer, "Pull from Muqamaat" panel for parent heads, review-and-submit screen, success screen with reference number and PDF download.
- **Compliance grid (observers):** units as rows, periods as columns, status chips in cells; tap a cell to open the report read-only; "Nudge all overdue" button.
- **Secretariat inbox:** split view with a filterable list on the left and the report on the right; keyboard shortcuts (J/K next/previous, A acknowledge, R return).
- **Template builder:** three panes: field palette, canvas with sections, properties panel; top bar with version badge, preview by level, and Publish.
- **Windows calendar:** month view of windows by report type, with drag to adjust dates and a side panel for extensions.
- **Organization tree:** expandable tree with search, vacant-head badges, drag to move units (with confirm), and a bulk import wizard.
- **Dashboards:** KPI tiles, compliance trend, indicator charts, drill-down breadcrumbs (National › Zone 3 › Dila 12), map view.
- **History:** list with filters, multi-select for bulk download, period comparison view.

### 10.4 Visual system

Design tokens for colour, type, spacing and radius in Tailwind config, with light and dark themes and organization brand colour as the accent. Inter or system UI font, 16 px base on mobile. Empty, loading (skeletons) and error states designed for every screen. Motion limited to under 200 ms and disabled when the user prefers reduced motion.

### 10.5 Design deliverables

User flows per persona, low-fidelity wireframes, a Figma component library matching shadcn/ui, high-fidelity mock-ups of the screens above, a clickable prototype of the Muqam submission flow, and usability tests with 6 real heads before build sign-off.

## 11. Testing, release plan and open questions

Release in four phases and launch to all units at once, after a full dress rehearsal in staging with the Secretariat and a sample of heads.

### 11.1 Testing strategy

- Unit tests: xUnit for domain rules (window states, aggregation, permission policies); Vitest for frontend logic and the shared expression evaluator.
- Integration tests: Testcontainers (PostgreSQL, Redis) running the API end to end, including the scheduler jobs.
- End-to-end tests: Playwright covering each persona's main flow on desktop and mobile viewports.
- Contract tests: generated OpenAPI client compiled in CI; breaking changes fail the build.
- Performance tests: k6 simulating the deadline spike in NFR-PRF-03.
- Security: OWASP ZAP baseline scan in CI, dependency scanning, and an external penetration test before national launch.
- Accessibility: axe-core in Playwright plus a manual screen-reader pass.
- User acceptance: the Secretariat signs off scripted scenarios in staging before each phase.

### 11.2 Release roadmap

The phases below each end with a gate the Secretariat must pass before the next starts.

&#91;embedded content: release roadmap · 4 phases, 3 gates\]

Durations are planning estimates for a team of 2 backend, 2 frontend, 1 designer and 1 QA; they should be re-estimated after Foundation.

### 11.3 Acceptance criteria (samples)

- **AC-01** Given a Muqam Head with an open General Monthly window, when they submit a valid report, then it appears in the Secretariat inbox as Submitted, the parent Dila Head sees it read-only, and the head receives a reference number and PDF by email.
- **AC-02** Given a Dila Head viewing a child Muqam report, then no acknowledge, return or edit control is present and the API returns 403 if such an action is attempted.
- **AC-03** Given three of five Muqamaat have submitted, when the Dila Head clicks "Pull from Muqamaat", then summed fields equal the sum of the three submissions and the panel lists the two missing units.
- **AC-04** Given a template is republished as version 3, then reports submitted against version 2 still render and export exactly as submitted.
- **AC-05** Given the hard close has passed and no extension exists, then submitting returns `WINDOW_CLOSED` and the instance is marked Missed after the close job runs.
- **AC-06** Given a head's term ends on 31 December, then on 1 January they can no longer log in to that unit and the new head sees all past reports.

### 11.4 Decisions and open questions

| Question | Decision |
| --- | --- |
| Do Muqam reports need Dila approval? | No. Muqam reports go straight to National; Dila heads stay read-only. |
| Department officer access | Read-only access to their department's sections in every unit's report (FR-REV-005). |
| SMS and WhatsApp | Out of v1. Notifications and OTP use email, in-app and web push only. |
| Units at launch | All zones, dilaat, muqamaat and departments go live together. |
| Hosting | Public cloud. |
| Languages | English at launch. |
| Signatures on reports | Not required; the submit confirmation and audit trail are the record. |

Still open:

- [ ] Data retention period per report type.
- [ ] Exact unit and user counts, to size infrastructure.
- [ ] Cloud provider and region (Azure or AWS).
