import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

test("desktop shows the sidebar and hides the bottom tabs", async ({ page, isMobile }) => {
  test.skip(isMobile, "desktop-only assertion");

  await page.goto("/home");
  await expect(page.getByRole("link", { name: "Home" }).first()).toBeVisible();
  await expect(page.getByText("Agodi Muqam")).toBeVisible();
  await expect(page.locator("nav[aria-label='Primary']").last()).toBeHidden();
});

test("mobile shows the bottom tabs and hides the sidebar", async ({ page, isMobile }) => {
  test.skip(!isMobile, "mobile-only assertion");

  await page.goto("/home");
  await expect(page.getByRole("link", { name: "Me", exact: true })).toBeVisible();
  await expect(page.getByText("Agodi Muqam")).toBeHidden();
});

test("home shell has no detectable accessibility violations", async ({ page }) => {
  await page.goto("/home");
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations).toEqual([]);
});
