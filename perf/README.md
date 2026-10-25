# Performance checks (M12.1)

Two checks cover the performance requirements in `docs/SRS.md`:

| Check | Requirement | Pass condition |
| --- | --- | --- |
| `k6/deadline-spike.js` | NFR-PRF-01, NFR-PRF-03 | reads p95 < 300 ms, saves and submissions p95 < 600 ms, no failed requests, while 5,000 heads submit at up to 50 a second |
| `lighthouse.sh` + `frontend/lighthouserc.cjs` | NFR-PRF-02 | median LCP < 2.5 s on Lighthouse's mobile profile (mid-range Android, 4x CPU slowdown, slow 4G) for login, Home, My reports and the report form |

Results from each recorded run go in `results/`.

## 1. Seed the load-test organization

`backend/tools/LoadTestSeeder` creates a throwaway organization next to your real data:

- 5,000 units coded `LT…` (10 zones, 100 dilaat, 4,890 muqamaat), each with a head;
- the users `lt-…@loadtest.invalid`, all sharing one password;
- a "Load test monthly report" type and a window that opened an hour ago and is due in two hours.

It only runs against a database on localhost.

```bash
docker compose up -d db redis
dotnet ef database update -p backend/src/HRS.Infrastructure -s backend/src/HRS.Api   # if not migrated yet
dotnet run --project backend/tools/LoadTestSeeder -- seed --units 5000 --out perf/k6/data/heads.csv
```

Seed again before every run. Each run replaces the previous load-test window, because reference
numbers are per period and unit, and a head can submit only once per window.

To remove everything afterwards, run `dotnet run --project backend/tools/LoadTestSeeder -- cleanup`.

## 2. Start the API like production

Use a Release build. Raise the sign-in rate limit for this run only: 5,000 sign-ins come from one
IP, and the production limit of 750 per 5 minutes per IP would refuse them.

```bash
dotnet build backend/src/HRS.Api -c Release
ASPNETCORE_ENVIRONMENT=Development RateLimiting__Auth__PermitLimit=1000000 \
  Serilog__MinimumLevel__Default=Warning \
  dotnet run --project backend/src/HRS.Api -c Release --no-build --no-launch-profile --urls http://0.0.0.0:8080
```

## 3. Run the deadline spike

k6 runs from its Docker image, so nothing needs installing. On Docker Desktop the API is reachable
at `host.docker.internal`.

```bash
docker run --rm -v "$PWD/perf/k6:/scripts" -w /scripts \
  -e BASE_URL=http://host.docker.internal:8080 \
  grafana/k6:1.3.0 run --summary-export out/deadline-spike.json deadline-spike.js
```

Each head signs in, opens Home and the report, saves the draft, checks the review warnings and
submits. Arrivals ramp to 50 a second over two minutes, and then hold until all 5,000 heads have
submitted, which takes about 2 minutes 40 seconds. The Secretariat refreshes the inbox, overview
and compliance dashboard five times a second throughout.

k6 exits non-zero if any threshold is missed. The summary shows a trend for each endpoint
(`endpoint_…`).

You can tune a run with these environment variables:

| Variable | Default | Controls |
| --- | --- | --- |
| `PEAK_RATE` | `50` | peak arrival rate, in submissions per second |
| `RAMP_SECONDS` | `120` | how long arrivals take to reach the peak |
| `HEADS_LIMIT` | all rows | number of heads that submit, for a smoke run |
| `PASSWORD` | `LoadTest-Password-2026` | the seeded users' shared password |

## 4. Lighthouse (LCP)

Build the frontend with the API proxied, since there's no Traefik locally. Then run the script, which
signs a seeded muqam head in and runs Lighthouse CI three times per page.

```bash
API_PROXY_URL=http://localhost:8080 pnpm -C frontend build
perf/lighthouse.sh
```

Reports land in `frontend/lighthouse-report/`. The script needs Chrome and Node, and it fetches
`@lhci/cli` with `npx`.
