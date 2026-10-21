import { describe, expect, it } from "vitest";
import { isNavItemActive, SECRETARIAT_NAV_ITEMS } from "./nav-items";

const overview = SECRETARIAT_NAV_ITEMS.find((item) => item.key === "overview")!;
const inbox = SECRETARIAT_NAV_ITEMS.find((item) => item.key === "inbox")!;

describe("isNavItemActive", () => {
  it("keeps the exact Overview item from matching every Secretariat page", () => {
    expect(isNavItemActive(overview, "/secretariat")).toBe(true);
    expect(isNavItemActive(overview, "/secretariat/inbox")).toBe(false);
  });

  it("still matches nested routes for ordinary items", () => {
    expect(isNavItemActive(inbox, "/secretariat/inbox")).toBe(true);
    expect(isNavItemActive(inbox, "/secretariat/inbox/123")).toBe(true);
    expect(isNavItemActive(inbox, "/secretariat/inboxes")).toBe(false);
  });
});
