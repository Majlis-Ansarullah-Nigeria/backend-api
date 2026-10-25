// M12.1 — the deadline spike of NFR-PRF-03, checked against NFR-PRF-01's latency budgets.
//
// Every seeded head (perf/k6/data/heads.csv, from backend/tools/LoadTestSeeder) does what the
// app does in the last hours before a deadline: signs in, opens Home, opens the report, saves the
// draft, checks the review warnings and submits. Arrivals ramp to 50 submissions a second
// (NFR-PRF-03's burst) and every head submits exactly once. Alongside, the Secretariat keeps
// refreshing the inbox, overview and compliance dashboard (SECRETARIAT_RATE, default once a second).
//
// Thresholds (the run fails if any is missed):
//   reads   p95 < 300 ms   (NFR-PRF-01)
//   writes  p95 < 600 ms   (NFR-PRF-01: saves and submissions)
//   no failed requests, every check passes (NFR-PRF-03: "without errors")
// Sign-in is measured and must succeed, but has no latency budget: password hashing (PBKDF2,
// NFR-SEC-02) is deliberately slow.
//
// Run: see perf/README.md.
import http from "k6/http";
import { check, fail } from "k6";
import exec from "k6/execution";
import { SharedArray } from "k6/data";
import { Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const PASSWORD = __ENV.PASSWORD || "LoadTest-Password-2026";
const PEAK_RATE = Number(__ENV.PEAK_RATE || 50);
const SECRETARIAT = __ENV.SECRETARIAT || "lt-secretariat@loadtest.invalid";
// Secretariat page loads a second (inbox, overview and compliance dashboard each time). One a
// second is a generous stand-in for a handful of secretaries refreshing through the deadline.
const SECRETARIAT_RATE = Number(__ENV.SECRETARIAT_RATE ?? 1);

// The seeder's CSV has no quoting or embedded commas, so a plain split is enough.
const heads = new SharedArray("heads", () => {
  const [header, ...rows] = open(__ENV.HEADS || "./data/heads.csv").trim().split(/\r?\n/);
  const columns = header.split(",");
  return rows.map((row) => Object.fromEntries(row.split(",").map((value, i) => [columns[i], value])));
});
const total = Number(__ENV.HEADS_LIMIT || heads.length);

// Arrival stages: a 2-minute ramp to the peak, then the peak until every head has submitted.
const rampSeconds = Number(__ENV.RAMP_SECONDS || 120);
const rampArrivals = (PEAK_RATE * rampSeconds) / 2;
const peakSeconds = Math.max(1, Math.ceil((total - rampArrivals) / PEAK_RATE));

export const options = {
  scenarios: {
    submitters: {
      executor: "ramping-arrival-rate",
      exec: "submitReport",
      startRate: 1,
      timeUnit: "1s",
      preAllocatedVUs: PEAK_RATE * 4,
      maxVUs: PEAK_RATE * 20,
      stages: [
        { target: PEAK_RATE, duration: `${rampSeconds}s` },
        { target: PEAK_RATE, duration: `${peakSeconds}s` },
      ],
    },
    secretariat: {
      executor: "constant-arrival-rate",
      exec: "secretariatReads",
      rate: Math.max(SECRETARIAT_RATE, 1),
      timeUnit: "1s",
      duration: `${rampSeconds + peakSeconds}s`,
      preAllocatedVUs: 10,
      maxVUs: 40,
    },
  },
  thresholds: {
    "http_req_duration{kind:read}": ["p(95)<300"],
    "http_req_duration{kind:write}": ["p(95)<600"],
    http_req_failed: ["rate==0"],
    checks: ["rate==1"],
  },
  summaryTrendStats: ["avg", "med", "p(90)", "p(95)", "p(99)", "max"],
};

// One trend per endpoint, so the summary shows which call is slow, not just the aggregate.
const ENDPOINTS = [
  "POST /auth/login",
  "GET /me",
  "GET /me/reports",
  "GET /me/notifications/unread-count",
  "GET /reports/{id}",
  "GET /reports/{id}/previous-answers",
  "PUT /reports/{id}",
  "GET /reports/{id}/review-warnings",
  "POST /reports/{id}/submit",
  "GET /secretariat/inbox",
  "GET /secretariat/overview",
  "GET /dashboards/compliance",
];
const trends = Object.fromEntries(
  ENDPOINTS.map((name) => [name, new Trend(`endpoint_${name.replace(/[^a-z]+/gi, "_").replace(/^_|_$/g, "").toLowerCase()}`, true)]),
);

function timed(res, name) {
  trends[name].add(res.timings.duration);
  return res;
}

function signIn(email) {
  const res = http.post(`${BASE_URL}/api/v1/auth/login`, JSON.stringify({ identifier: email, password: PASSWORD }), {
    headers: { "Content-Type": "application/json" },
    tags: { kind: "auth", name: "POST /auth/login" },
  });
  timed(res, "POST /auth/login");
  const cookie = res.cookies.access_token && res.cookies.access_token[0];
  if (!check(res, { "signed in": (r) => r.status === 200 && Boolean(cookie) })) {
    fail(`sign-in failed for ${email}: ${res.status} ${res.body}`);
  }
  // Auth cookies are Secure and this run is plain HTTP, so k6's jar won't send them back;
  // pass the access token explicitly instead.
  return { Cookie: `access_token=${cookie.value}` };
}

function read(url, name, auth, okStatuses = [200]) {
  const res = timed(http.get(`${BASE_URL}${url}`, { headers: auth, tags: { kind: "read", name } }), name);
  check(res, { [`${name} ok`]: (r) => okStatuses.includes(r.status) });
  return res;
}

export function setup() {
  return { secretariat: signIn(SECRETARIAT) };
}

export function submitReport() {
  const index = exec.scenario.iterationInTest;
  if (index >= total) {
    return; // every head has submitted; the scenario winds down
  }
  const head = heads[index];
  const auth = signIn(head.email);
  const report = `/api/v1/reports/${head.windowId}`;
  const unit = `unit=${head.unitId}`;

  read("/api/v1/me", "GET /me", auth);
  read("/api/v1/me/reports", "GET /me/reports", auth);
  read("/api/v1/me/notifications/unread-count", "GET /me/notifications/unread-count", auth);
  const current = read(`${report}?${unit}`, "GET /reports/{id}", auth);
  // 204 when the unit has no earlier report — the first window it reports in.
  read(`${report}/previous-answers?${unit}`, "GET /reports/{id}/previous-answers", auth, [200, 204]);

  const answers = {
    members: 50 + (index % 400),
    meetings: 1 + (index % 6),
    funds: 10000 + index * 10,
    highlights: `Load test report ${index}: activities held as planned.`,
  };
  const saved = timed(http.put(`${BASE_URL}${report}?${unit}`, JSON.stringify({ answers }), {
    headers: { ...auth, "Content-Type": "application/json", "If-Match": current.headers.Etag || "*" },
    tags: { kind: "write", name: "PUT /reports/{id}" },
  }), "PUT /reports/{id}");
  if (!check(saved, { "draft saved": (r) => r.status === 200 })) {
    console.error(`save failed for ${head.unitCode} (${head.level}): ${saved.status} ${saved.body}`);
  }

  read(`${report}/review-warnings?${unit}`, "GET /reports/{id}/review-warnings", auth);

  const submitted = timed(http.post(`${BASE_URL}${report}/submit?${unit}`, JSON.stringify({ confirmed: true }), {
    headers: { ...auth, "Content-Type": "application/json", "Idempotency-Key": `lt-${head.windowId}-${head.unitId}` },
    tags: { kind: "write", name: "POST /reports/{id}/submit" },
  }), "POST /reports/{id}/submit");
  if (!check(submitted, { submitted: (r) => r.status === 200 })) {
    console.error(`submit failed for ${head.unitCode} (${head.level}): ${submitted.status} ${submitted.body}`);
  }
}

export function secretariatReads(data) {
  if (SECRETARIAT_RATE <= 0) {
    return;
  }
  read("/api/v1/secretariat/inbox", "GET /secretariat/inbox", data.secretariat);
  read("/api/v1/secretariat/overview", "GET /secretariat/overview", data.secretariat);
  read("/api/v1/dashboards/compliance", "GET /dashboards/compliance", data.secretariat);
}

