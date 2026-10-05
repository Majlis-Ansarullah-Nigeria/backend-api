# ADR 0006 — RustFS for local S3-compatible dev storage

Status: accepted

## Context
`docker-compose.yml`'s dev stack used `minio/minio:latest` as a local S3-compatible target for the `IFileStorage` abstraction (attachments, exports). MinIO stopped publishing free Community Edition images: removed from Docker Hub in 2026, and the `quay.io/minio/minio` fallback was gated shortly after. `docker compose up` failed outright (`pull access denied`).

## Decision
Replace the `minio` service with `rustfs/rustfs:latest` (Apache-2.0, actively maintained, anonymously pullable). Same ports (9000 API, 9001 console), same shape: access/secret key env vars, a named volume for data. Service renamed `storage` to stop naming it after a specific vendor.

## Consequences
- Local dev and CI's Testcontainers-backed storage tests (when they land) point at `storage:9000` / `localhost:9000` exactly as they pointed at `minio:9000` before.
- Production is unaffected — it never used MinIO; it uses real S3/R2/B2 per `.env.example`.
- If RustFS's free distribution ever breaks the same way, re-evaluate from the alternatives already surveyed (Garage, SeaweedFS) rather than assuming any single vendor's "free forever" container image.
