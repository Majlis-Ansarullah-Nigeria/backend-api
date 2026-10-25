#!/usr/bin/env bash
# M12.1 — runs Lighthouse CI (NFR-PRF-02) against a production build of the frontend, signed in as
# a seeded head. Expects: the API on :8080, `pnpm build` done with API_PROXY_URL=http://localhost:8080,
# and perf/k6/data/heads.csv from backend/tools/LoadTestSeeder. See perf/README.md.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
api="${API_URL:-http://localhost:8080}"
password="${PASSWORD:-LoadTest-Password-2026}"
heads="${HEADS:-$root/perf/k6/data/heads.csv}"

# A muqam head: the most common user, on the busiest screens.
IFS=, read -r email unit_id _ _ window_id < <(grep ',Muqam,' "$heads" | head -n 1)

cookie="$(curl -fsS -c - -o /dev/null -H 'Content-Type: application/json' \
  -d "{\"identifier\":\"$email\",\"password\":\"$password\"}" "$api/api/v1/auth/login" \
  | awk '$6 == "access_token" { print $7 }')"
if [[ -z "$cookie" ]]; then
  echo "Sign-in as $email failed" >&2
  exit 1
fi

export LHCI_COOKIE="access_token=$cookie"
export LHCI_REPORT_PATH="/reports/$window_id/edit?unit=$unit_id"

cd "$root/frontend"
# Standalone output: serve the built server with its static assets alongside.
cp -r .next/static .next/standalone/.next/ 2>/dev/null || true
cp -r public .next/standalone/ 2>/dev/null || true
PORT=3000 node .next/standalone/server.js > /tmp/hrs-lighthouse-web.log 2>&1 &
web_pid=$!
trap 'kill $web_pid 2>/dev/null || true' EXIT
for _ in $(seq 1 30); do
  curl -fsS -o /dev/null http://localhost:3000/login && break
  sleep 1
done

npx --yes @lhci/cli@0.15.1 autorun --config=./lighthouserc.cjs
