# HRS — Hierarchical Reporting Service

Reporting platform for a hierarchical organization: National → Zones → Dilaat → Muqamaat, plus National departments. Unit heads submit structured reports to the National Secretariat inside submission windows the Secretariat sets. Parent heads (Dila, Zone) see child reports read-only.

## Sources of truth (read before building a feature)
- Requirements: `docs/SRS.md`. Every requirement has an ID (FR-SUB-007, NFR-SEC-02, BR-WIN-03). Cite IDs in commits and PRs.
- Build order: `docs/PLAN.md`. Work one milestone slice at a time and tick its boxes when done.
- Architecture rules: `docs/architecture.md`. Decisions: `docs/adr/`. Add an ADR for any change to those rules.
- UI: `docs/design/README.md` maps every screen to its mockup and lists the design tokens. Match the mockups.

## Repo layout
- `backend/` — .NET 10, Clean Architecture, modular by feature. See `backend/CLAUDE.md`.
- `frontend/` — Next.js (App Router, TypeScript). See `frontend/CLAUDE.md`.
- `docker-compose.yml` — local dev stack (Postgres, Redis, Mailpit, RustFS, ClamAV).
- `docker-compose.dokploy.yml` — production stack deployed by Dokploy from `main`.
- `.github/workflows/` — CI on every PR; deploy to Dokploy after CI passes on `main`.

## Commands
- Dev services: `docker compose up -d` (Postgres :5432, Redis :6379, Mailpit UI :8025, RustFS console :9001)
- Backend: `dotnet build backend/HRS.sln` · `dotnet test backend/HRS.sln` · run API `dotnet run --project backend/src/HRS.Api` (:8080, Swagger at /scalar)
- Frontend: `pnpm -C frontend dev` (:3000) · `pnpm -C frontend lint` · `pnpm -C frontend typecheck` · `pnpm -C frontend test`
- API client: `pnpm -C frontend api:generate` after any backend contract change (API must be running).
- Before committing, run the `verify` skill.

## Non-negotiables
- Authorization is enforced in the backend for every request. The UI only hides what the API already forbids.
- Reports belong to a Unit, never to a User. Heads change; history stays with the unit.
- Observers (parent Dila/Zone heads) are read-only. No mutating endpoint may accept an observer.
- Published template versions are immutable. Reports always render with the version they were filled against.
- Store all times in UTC; display in the organization time zone (default Africa/Lagos).
- The server clock decides whether a window is open (BR-WIN-07).
- No secrets in the repo. Only `.env.example` files are committed.
- v1 has no SMS or WhatsApp. Email, in-app and web push only.
- English only at launch, but all UI strings go through the i18n layer.

## How to work
1. Read the requirement IDs involved, the matching mockup, and `docs/PLAN.md`. State a short plan before editing.
2. Build vertical slices: domain → application → API endpoint → tests → regenerate client → UI → UI tests.
3. Every change ships with tests. Never delete or weaken a test to make it pass.
4. Keep changes scoped to the task. Note unrelated problems in your summary instead of fixing them.
5. Commits use Conventional Commits with requirement IDs: `feat(submissions): autosave drafts [FR-SUB-003]`.
6. If a requirement is ambiguous, stop and ask. Do not invent business rules.

## Glossary
Unit = any node (National, Zone, Dila, Muqam, Department). Head = user appointed to a unit. Report Type → Template Version → Submission Window → Report Instance (one per unit per window). Recipient = National Secretariat (can act). Observer = parent head (read-only). Statuses: NotStarted, Draft, Submitted, InReview, Returned, Acknowledged, Missed; IsLate is a flag. Full rules: the `hrs-domain` skill.
