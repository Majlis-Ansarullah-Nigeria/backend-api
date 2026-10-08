import { describe, expect, it } from "vitest";
import { countdownText } from "./countdown";

const Now = new Date("2026-10-01T00:00:00Z");

describe("countdownText", () => {
  it("says due today when the due date is today", () => {
    expect(countdownText("2026-10-01T18:00:00Z", Now)).toBe("Due today");
  });

  it("says due tomorrow when one day away", () => {
    expect(countdownText("2026-10-02T00:00:00Z", Now)).toBe("Due tomorrow");
  });

  it("counts days left for anything further out", () => {
    expect(countdownText("2026-10-09T00:00:00Z", Now)).toBe("8 days left");
  });

  it("says overdue today when the due date was earlier today", () => {
    expect(countdownText("2026-10-01T00:00:00Z", new Date("2026-10-01T12:00:00Z"))).toBe("Overdue today");
  });

  it("says overdue by 1 day", () => {
    expect(countdownText("2026-09-30T00:00:00Z", Now)).toBe("Overdue by 1 day");
  });

  it("says overdue by N days", () => {
    expect(countdownText("2026-09-24T00:00:00Z", Now)).toBe("Overdue by 7 days");
  });
});
