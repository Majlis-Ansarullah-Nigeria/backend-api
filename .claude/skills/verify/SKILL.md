---
name: verify
description: Build, lint, type-check and test the parts of the repo that changed. Run before every commit and whenever asked to check the work.
allowed-tools: Bash(git diff *) Bash(git status *) Bash(dotnet build *) Bash(dotnet test *) Bash(dotnet format *) Bash(pnpm -C frontend *)
---

# Verify

## Changed files
!`git status --short`

## Steps
Decide from the changed files which side to check. Run both if shared files, Docker files or workflows changed.

Backend (anything under `backend/`):
1. `dotnet format backend/HRS.sln --verify-no-changes` (if it fails, run `dotnet format backend/HRS.sln` and re-check)
2. `dotnet build backend/HRS.sln -warnaserror`
3. `dotnet test backend/HRS.sln --no-build` (needs Docker for Testcontainers)

Frontend (anything under `frontend/`):
1. `pnpm -C frontend lint`
2. `pnpm -C frontend typecheck`
3. `pnpm -C frontend test --run`
4. If routes or layouts changed: `pnpm -C frontend build` and check first-load JS of report pages stays under 200 KB

Report each step as pass or fail. On a failure, fix the cause and rerun that step; never skip or disable a test, lint rule or type check to get green.
