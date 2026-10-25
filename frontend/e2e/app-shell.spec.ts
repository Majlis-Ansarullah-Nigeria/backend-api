import { expect, test } from "@playwright/test";
import { PASSWORD } from "./global-setup";
import { authState, fixtures } from "./fixtures";

test.use({ storageState: authState("draft-head") });

test("desktop shows the sidebar and hides the bottom tabs", async ({ page, isMobile }) => {
  test.skip(isMobile, "desktop-only assertion");

  await page.goto("/home");
  await expect(page.getByRole("link", { name: "Home" }).first()).toBeVisible();
  // The signed-in head and the unit they act as, from GET /me — not placeholders.
  const sidebar = page.getByRole("complementary");
  await expect(sidebar.getByText("Load Test Muqam 1-1-1", { exact: true })).toBeVisible();
  await expect(sidebar.getByText("Muqam Head")).toBeVisible();
  await expect(sidebar.getByText("Head of Load Test Muqam 1-1-1")).toBeVisible();
  await expect(page.locator("nav[aria-label='Primary']").last()).toBeHidden();
});

test("mobile shows the bottom tabs and hides the sidebar", async ({ page, isMobile }) => {
  test.skip(!isMobile, "mobile-only assertion");

  await page.goto("/home");
  await expect(page.getByRole("link", { name: "Me", exact: true })).toBeVisible();
  await expect(page.getByRole("complementary")).toBeHidden();
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

test.describe("sign in and out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test("a head signs in through the form, sees their own name, and signs out", async ({ page, isMobile }) => {
    test.skip(isMobile, "the sign-out button is in the desktop sidebar");

    await page.goto("/login");
    await page.getByLabel("Email").fill(fixtures().signOutEmail);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByRole("complementary").getByText("Head of Load Test Zone 1")).toBeVisible();

    await page.getByRole("button", { name: "Sign out" }).click();
    await expect(page).toHaveURL(/\/login/);
  });
});
