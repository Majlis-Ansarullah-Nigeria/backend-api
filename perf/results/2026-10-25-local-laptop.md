# 2026-10-25 — local laptop, deadline spike (partial, M12.1 paused)

Machine: one 12-thread Windows laptop running everything at once — Docker Desktop (Postgres 17,
Redis), the API (Release build) and k6 (`grafana/k6:1.3.0`, up to ~1,000 VUs). Not
production-like; read the numbers as cost per request, not as capacity.

## Results

| Run | Heads/s | Reads p95 | Writes p95 | Errors |
| --- | --- | --- | --- | --- |
| Heads only, steady | 15 | 115 ms | 252 ms | 0 (after re-seeding fix) |
| Heads + Secretariat 1/s, ramp to 50 | peaked ~11 sustained | 5.3 s | 39.5 s | 0 failed, 2,624 iterations dropped |

Uncontended cost per request (one head at a time): sign-in ~110 ms (PBKDF2, 100k iterations,
NFR-SEC-02), reads 10–15 ms, draft save ~18 ms, submit ~45 ms.

At 15 heads/s the API used ~3.1 cores (~205 ms CPU per head flow, about half of it sign-in).
Extrapolated, 50 heads/s needs ~10 API cores plus Postgres — beyond this laptop, which saturates
around 11–15 heads/s. NFR-PRF-01/03 are **not yet shown to pass**.

## Findings to act on

1. **Connection pool vs `max_connections`.** Npgsql's default pool (100 per process) equals
   Postgres's default `max_connections` (100). Under load the API alone took all 100, locking out
   the Worker, migrator and admin sessions ("sorry, too many clients already"). Production shares
   one `DB_CONNECTION` between API and Worker. Needs a per-process `Maximum Pool Size` (or a higher
   `max_connections`) before launch.
2. **Reference-number collisions return 500.** References are `{category code}-{period}-{unit}`
   (e.g. `GMR-2026-10-Z03D12M04`), so two General-Monthly report types — or two windows of one
   type in the same month — give the same reference for a unit, and the second submit fails with
   a unique-index 500. Needs a decision on the type code (SRS says `{TYPECODE}` per report type).
3. **Secretariat screens scale with the whole organization.** Inbox, overview and compliance load
   every unit and every head assignment per request (~5,000 rows each); at 5 refreshes/s they
   alone saturated the box. Fine at realistic refresh rates, but worth caching if the Secretariat
   grows.
4. **Sign-in dominates CPU** when every head signs in right before submitting. The script models
   the worst case (sign-in and submit within a second); real heads sign in minutes before.

## Still to do for M12.1

- CPU profile (dotnet-trace) of the non-sign-in requests for avoidable work.
- Run on production-like hardware (or staging) with k6 on a separate machine.
- Run `perf/lighthouse.sh` (NFR-PRF-02) and check the new CI `lighthouse` job once.
