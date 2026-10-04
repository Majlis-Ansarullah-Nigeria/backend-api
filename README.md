# HRS — Hierarchical Reporting Service

Monthly and ad-hoc reporting from every Muqam, Dila, Zone and National department to the National Secretariat.

- Requirements: `docs/SRS.md` · Build plan: `docs/PLAN.md` · Architecture: `docs/architecture.md` · Design: `docs/design/README.md`
- Backend: .NET 10 Clean Architecture (`backend/`) · Frontend: Next.js (`frontend/`)
- Deploy: GitHub → CI → Dokploy (Docker Compose)

## 1. Set up the repo
1. Create a private GitHub repo and copy this kit into it. Commit to `main`, then create `develop`.
2. Replace `docs/SRS.md` with the Markdown export of the SRS doc (instructions inside the file).
3. Export the mockup boards you'll work on first as PNG into `docs/design/screens/`.
4. Protect `main` and `develop`: require a PR and the CI checks `backend`, `frontend`, `docker`.

## 2. Build with Claude Code
Install Claude Code, open a terminal at the repo root and run `claude`. Useful first prompts:

```
Read CLAUDE.md, docs/architecture.md and docs/PLAN.md. Then /implement-requirement M0.1
```
Then continue milestone by milestone: `/implement-requirement M0.2`, … For a single requirement: `/implement-requirement FR-SUB-007`.

What's in `.claude/`:
| Item | Kind | Use |
| --- | --- | --- |
| `hrs-domain` | skill (auto) | Business rules: hierarchy, routing, statuses, windows |
| `backend-feature` | skill (auto in backend/) | Clean Architecture use case recipe + code templates |
| `ef-migration` | skill | `/ef-migration AddReportRowVersion` |
| `frontend-feature` | skill (auto in frontend/) | Screen/form recipe matching the mockups |
| `api-sync` | skill | Regenerate the typed API client |
| `implement-requirement` | skill (manual) | End-to-end slice for an FR-ID or plan item |
| `verify` | skill | Build, lint, typecheck, test what changed; Claude runs it before commits |
| `deploy` | skill (manual) | Tag and release `main`, check health |
| `code-reviewer`, `security-reviewer` | subagents | "Use the code-reviewer agent on this branch" |
| `settings.json` | permissions | Pre-approves build/test commands, blocks reading `.env` |

Tips: start each milestone in a fresh session (`/clear`), use plan mode for anything non-trivial, and ask for the `code-reviewer` and `security-reviewer` agents before opening a PR.

## 3. Run locally
```
docker compose up -d                       # Postgres, Redis, Mailpit, MinIO, ClamAV
dotnet run --project backend/src/HRS.Api   # http://localhost:8080
pnpm -C frontend install && pnpm -C frontend dev   # http://localhost:3000
```
Emails land in Mailpit at http://localhost:8025.

## 4. Deploy with Dokploy
1. **Server:** a cloud VM (4 vCPU, 8 GB RAM is a reasonable start) with Dokploy installed. Point DNS for `reports.<your-domain>` at it.
2. **GitHub:** in Dokploy, connect your GitHub account (Settings › Git) so it can read the private repo.
3. **Project:** create a project `hrs` with two environments' worth of services, or two projects `hrs-staging` and `hrs-prod`.
4. **Database:** add a PostgreSQL database service. Configure scheduled backups to an S3 destination. Copy its internal connection string.
5. **Compose app:** add a Docker Compose service → provider GitHub → this repo → branch `main` (staging: `develop`) → compose path `docker-compose.dokploy.yml`. Turn Auto Deploy off (the GitHub workflow deploys after CI passes).
6. **Environment:** paste the keys from `.env.example` with real values in the Environment tab.
7. **Domains:** on the same host add `web` → port 3000, path `/`; `api` → port 8080, path `/api`; `api` → port 8080, path `/hubs`. Enable HTTPS (Let's Encrypt).
8. **First deploy:** click Deploy once in Dokploy and watch the logs. `migrator` must exit 0 before `api` starts.
9. **CI deploy hook:** create a Dokploy API token. In GitHub create environments `production` and `staging`; add secrets `DOKPLOY_URL`, `DOKPLOY_TOKEN`, and variables `DOKPLOY_COMPOSE_ID`, `APP_PUBLIC_URL` per environment. From now on, merging to `main` deploys production after CI is green.

Check the current Dokploy docs for exact UI labels and the deploy API: https://docs.dokploy.com
