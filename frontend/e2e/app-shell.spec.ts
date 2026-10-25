import { expect, test } from "@playwright/test";
import { authState } from "./fixtures";

test.use({ storageState: authState("draft-head") });

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

test("Ctrl+K opens the search palette and Escape closes it", async ({ page, isMobile }) => {
  test.skip(isMobile, "keyboard shortcut");

  await page.goto("/home");
  await page.keyboard.press("Control+k");
  const palette = page.getByRole("dialog", { name: "Search and quick actions" });
  await expect(palette).toBeVisible();
  await expect(page.getByRole("combobox", { name: "Search units, reports, people" })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(palette).toBeHidden();
});

test("the first Tab reaches a skip link that jumps to the main content", async ({ page, isMobile }) => {
  test.skip(isMobile, "keyboard navigation");

  await page.goto("/home");
  await page.keyboard.press("Tab");
  const skip = page.getByRole("link", { name: "Skip to main content" });
  await expect(skip).toBeFocused();
  await expect(skip).toBeVisible();
  await page.keyboard.press("Enter");
  await expect(page.locator("main#main-content")).toBeFocused();
});
