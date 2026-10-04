---
name: deploy
description: Release the current main branch to production on Dokploy and check that it is healthy.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Bash(curl -s *)
---

# Deploy to production

Production deploys run from `main` through `.github/workflows/deploy.yml` (after CI passes) or Dokploy auto-deploy. This skill checks and tags a release.

1. Confirm you are on `main`, up to date, with a clean tree: `git status`, `git pull --ff-only`.
2. Confirm the latest CI run on `main` passed: `gh run list --branch main --workflow ci.yml --limit 1`.
3. List what ships since the last tag: `git log $(git describe --tags --abbrev=0)..HEAD --oneline`. Flag any migration under `backend/src/HRS.Infrastructure/Persistence/Migrations` and say whether it is backward compatible.
4. Ask the user to confirm the release before continuing.
5. Tag and push: `git tag -a vX.Y.Z -m "<summary>"` then `git push origin vX.Y.Z`.
6. Watch the deploy workflow: `gh run watch` on the latest `deploy.yml` run.
7. Health check: `curl -s https://<APP_DOMAIN>/api/health` must return Healthy, and the login page must load.
8. If unhealthy: in Dokploy, redeploy the previous successful deployment (Deployments tab), and report the failing logs. Migrations are forward-only; if one must be reverted, write a new migration.
