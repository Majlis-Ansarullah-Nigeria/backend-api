import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { request } from "@playwright/test";

/**
 * M12.3: signs real heads in for the accessibility suite. Seeds a seven-unit throwaway
 * organization with backend/tools/LoadTestSeeder (one zone, two dilaat, four muqamaat, all
 * coded LT…), then:
 *  - the first muqam head keeps a saved draft (report form, review screen);
 *  - the second muqam head submits (submitted screen, history, versions);
 *  - the head of their dila observes the submitted report (compliance grid, child report).
 * Needs the API on API_URL (default http://localhost:8080) and a local database.
 */
export const AUTH_DIR = path.join(__dirname, ".auth");
export const PASSWORD = "LoadTest-Password-2026";

export interface E2eFixtures {
  windowId: string;
  draftUnitId: string;
  submittedUnitId: string;
  submittedInstanceId: string;
  dilaUnitId: string;
  /** A head whose session the sign-out test may end without affecting the others. */
  signOutEmail: string;
}

interface HeadRow {
  email: string;
  unitId: string;
  unitCode: string;
  level: string;
  windowId: string;
}

const apiUrl = process.env.API_URL ?? "http://localhost:8080";

export default async function globalSetup() {
  mkdirSync(AUTH_DIR, { recursive: true });
  const csv = path.join(AUTH_DIR, "heads.csv");
  execFileSync(
    "dotnet",
    ["run", "--project", path.join(__dirname, "..", "..", "backend", "tools", "LoadTestSeeder"), "--", "seed", "--units", "7", "--out", csv],
    { stdio: "inherit" },
  );

  const [header, ...rows] = readFileSync(csv, "utf8").trim().split(/\r?\n/);
  const columns = header.split(",");
  const heads = rows.map((row) => Object.fromEntries(row.split(",").map((v, i) => [columns[i], v])) as unknown as HeadRow);
  const [draftHead, submittedHead] = heads.filter((h) => h.level === "Muqam");
  const dilaHead = heads.find((h) => h.level === "Dila" && submittedHead.unitCode.startsWith(h.unitCode))!;
  const windowId = submittedHead.windowId;

  const draft = await signIn(draftHead.email, "draft-head");
  await saveDraft(draft, windowId, draftHead.unitId, { members: 120, meetings: 3 });

  const submitter = await signIn(submittedHead.email, "submitted-head");
  await saveDraft(submitter, windowId, submittedHead.unitId, { members: 98, meetings: 4, funds: 45000, highlights: "Youth programme started." });
  const submitted = await submitter.post(`/api/v1/reports/${windowId}/submit?unit=${submittedHead.unitId}`, { data: { confirmed: true } });
  if (!submitted.ok()) {
    throw new Error(`Submit failed: ${submitted.status()} ${await submitted.text()}`);
  }
  const submittedInstanceId = ((await submitted.json()) as { id: string }).id;

  await signIn(dilaHead.email, "dila-head");

  const fixtures: E2eFixtures = {
    windowId,
    draftUnitId: draftHead.unitId,
    submittedUnitId: submittedHead.unitId,
    submittedInstanceId,
    dilaUnitId: dilaHead.unitId,
    signOutEmail: heads.find((h) => h.level === "Zone")!.email,
  };
  writeFileSync(path.join(AUTH_DIR, "fixtures.json"), JSON.stringify(fixtures, null, 2));
}

/** Signs in through the API and saves the session for the browser. The API's cookies are scoped
 * to the host, so they apply to the frontend on another localhost port too. */
async function signIn(email: string, name: string) {
  const context = await request.newContext({ baseURL: apiUrl });
  const response = await context.post("/api/v1/auth/login", { data: { identifier: email, password: PASSWORD } });
  if (!response.ok()) {
    throw new Error(`Sign-in failed for ${email}: ${response.status()} ${await response.text()}`);
  }
  await context.storageState({ path: path.join(AUTH_DIR, `${name}.json`) });
  return context;
}

async function saveDraft(context: Awaited<ReturnType<typeof request.newContext>>, windowId: string, unitId: string, answers: Record<string, unknown>) {
  const url = `/api/v1/reports/${windowId}?unit=${unitId}`;
  const current = await context.get(url);
  const response = await context.put(url, { data: { answers }, headers: { "If-Match": current.headers()["etag"] ?? "*" } });
  if (!response.ok()) {
    throw new Error(`Save failed: ${response.status()} ${await response.text()}`);
  }
}
