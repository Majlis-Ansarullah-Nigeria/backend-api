import { expect, test, type Page } from "@playwright/test";
import { authState, expectNoAxeViolations, fixtures } from "./fixtures";

/**
 * M12.3 / NFR-ACC-01: every head-facing screen, signed in as a real head, on desktop and mobile
 * (playwright.config.ts projects), checked against WCAG 2.2 AA with axe. Each screen waits for its
 * own heading, so the scan runs on loaded content, not a loading state.
 */
async function open(page: Page, url: string, heading: RegExp | string) {
  await page.goto(url);
  await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
}

test.describe("signed out", () => {
  for (const [url, heading] of [
    ["/login", /sign in/i],
    ["/login/forgot", /reset|forgot/i],
  ] as const) {
    test(`${url} has no accessibility violations`, async ({ page }) => {
      await open(page, url, heading);
      await expectNoAxeViolations(page);
    });
  }
});

test.describe("muqam head with a draft", () => {
  test.use({ storageState: authState("draft-head") });

  const screens = () => {
    const f = fixtures();
    return [
      ["home", "/home", /good (morning|afternoon|evening)|reports/i],
      ["my reports", "/reports", /my reports/i],
      ["report form", `/reports/${f.windowId}/edit?unit=${f.draftUnitId}`, /load test monthly report/i],
      ["review and submit", `/reports/${f.windowId}/review?unit=${f.draftUnitId}`, /review/i],
      ["notifications", "/notifications", /notifications/i],
      ["profile", "/profile", /profile/i],
    ] as const;
  };

  for (const name of ["home", "my reports", "report form", "review and submit", "notifications", "profile"]) {
    test(`${name} has no accessibility violations`, async ({ page }) => {
      const [, url, heading] = screens().find(([n]) => n === name)!;
      await open(page, url, heading);
      await expectNoAxeViolations(page);
    });
  }
});

test.describe("muqam head who submitted", () => {
  test.use({ storageState: authState("submitted-head") });

  const screens = () => {
    const f = fixtures();
    return [
      ["submitted", `/reports/${f.windowId}/submitted?unit=${f.submittedUnitId}`, /submitted/i],
      ["history", "/history", /history/i],
      ["version history", `/history/${f.submittedInstanceId}/versions`, /version/i],
      ["compare periods", "/history/compare", /compare/i],
    ] as const;
  };

  for (const name of ["submitted", "history", "version history", "compare periods"]) {
    test(`${name} has no accessibility violations`, async ({ page }) => {
      const [, url, heading] = screens().find(([n]) => n === name)!;
      await open(page, url, heading);
      await expectNoAxeViolations(page);
    });
  }
});

test.describe("dila head observing a muqam", () => {
  test.use({ storageState: authState("dila-head") });

  test("compliance grid has no accessibility violations", async ({ page }) => {
    await open(page, "/units", /my muqamaat/i);
    await expectNoAxeViolations(page);
  });

  test("child report has no accessibility violations", async ({ page }) => {
    const f = fixtures();
    await open(page, `/units/${f.submittedUnitId}/reports/${f.submittedInstanceId}`, /load test muqam/i);
    await expectNoAxeViolations(page);
  });
});
