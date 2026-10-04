---
name: api-sync
description: Regenerate the typed frontend API client from the backend OpenAPI document after any backend endpoint or DTO change, then fix type errors it causes.
allowed-tools: Bash(pnpm -C frontend api:generate) Bash(pnpm -C frontend typecheck) Bash(curl -s http://localhost:8080/openapi/v1.json*)
---

# Sync the API client

1. Make sure the API is running locally on :8080 (`dotnet run --project backend/src/HRS.Api`). Check with `curl -s http://localhost:8080/openapi/v1.json | head -c 200`.
2. Run `pnpm -C frontend api:generate` (orval reads `frontend/orval.config.ts`, writes `src/lib/api/generated`).
3. Run `pnpm -C frontend typecheck` and fix every caller broken by the contract change. Do not cast to `any` to silence errors.
4. If an endpoint was renamed or removed, search the frontend for its old hook name and update usages.
5. Commit the generated files together with the backend change so CI's contract check passes.
