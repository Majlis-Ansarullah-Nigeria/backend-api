# ADR 0003 — Same-origin cookie authentication

Status: accepted

## Decision
Serve the web app and the API on one domain. Traefik routes `/api/*` and `/hubs/*` to the API and everything else to Next.js. The API issues a short-lived access cookie and a rotating refresh cookie (httpOnly, Secure, SameSite=Lax). No tokens in browser storage.

## Consequences
- No CORS; CSRF handled with SameSite plus an anti-forgery header on mutating requests.
- Next.js server code calls the API on the internal network (`API_INTERNAL_URL`) and forwards the user's cookie.
- Local dev uses a Next.js rewrite of `/api/*` to `http://localhost:8080`.
