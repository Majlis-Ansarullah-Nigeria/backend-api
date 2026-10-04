# ADR 0004 — Deploy with Docker Compose on Dokploy

Status: accepted

## Decision
Production runs on a cloud VM managed by Dokploy. GitHub is the source; `docker-compose.dokploy.yml` defines `migrator`, `api`, `worker`, `web`, `redis` and `clamav`. PostgreSQL runs as a separate Dokploy database service so Dokploy's scheduled backups to S3 apply. Domains and TLS are configured in the Dokploy UI (Traefik + Let's Encrypt). GitHub Actions runs CI on every PR and triggers the Dokploy deploy API only after CI passes on `main`.

## Consequences
- One server is a single point of failure; acceptable for v1 with backups and a tested restore (NFR-AVL-02). Revisit for the 99.9% deadline-week target.
- Images are built on the Dokploy server; for faster deploys later, build in CI and push to GHCR.
- Migrations run as a one-shot container before the API starts.
- Staging is a second Dokploy compose app pointed at the `develop` branch.
