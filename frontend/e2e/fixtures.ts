import { readFileSync } from "node:fs";
import path from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";
import { AUTH_DIR, type E2eFixtures } from "./global-setup";

export function fixtures(): E2eFixtures {
  return JSON.parse(readFileSync(path.join(AUTH_DIR, "fixtures.json"), "utf8")) as E2eFixtures;
}

export const authState = (name: "draft-head" | "submitted-head" | "dila-head") => path.join(AUTH_DIR, `${name}.json`);

/** NFR-ACC-01: WCAG 2.2 AA. axe covers what can be checked automatically (labels, roles, names,
 * contrast, landmarks); keyboard order and screen-reader wording still need the manual pass. */
export async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  const summary = results.violations.map((v) => ({
    rule: v.id,
    impact: v.impact,
    help: v.help,
    targets: v.nodes.slice(0, 5).map((n) => n.target.join(" ")),
  }));
  expect(summary, JSON.stringify(summary, null, 2)).toEqual([]);
}
