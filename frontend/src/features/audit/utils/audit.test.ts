import { describe, expect, it } from "vitest";
import { auditTone, initials, jsonLines, rangeStart } from "./audit";

describe("auditTone", () => {
  it("tones actions by what they are", () => {
    expect(auditTone("report.submitted")).toBe("submitted");
    expect(auditTone("report.returned")).toBe("returned");
    expect(auditTone("auth.sign_in_failed")).toBe("returned");
    expect(auditTone("window.extended")).toBe("late");
    expect(auditTone("template.published")).toBe("in-review");
    expect(auditTone("role.assigned")).toBe("acknowledged");
    expect(auditTone("export.downloaded")).toBe("draft");
  });
});

describe("initials", () => {
  it("takes the first and last name's initials", () => {
    expect(initials("Mariam Okafor")).toBe("MO");
    expect(initials("System")).toBe("S");
    expect(initials("  ")).toBe("?");
  });
});

describe("jsonLines", () => {
  it("lists an object's fields as key: value lines", () => {
    expect(jsonLines('{"dueAt":"2026-10-12","reason":"No head","units":["Iwo Road"],"note":null}')).toEqual([
      "dueAt: 2026-10-12",
      "reason: No head",
      'units: ["Iwo Road"]',
      "note: —",
    ]);
    expect(jsonLines(null)).toEqual([]);
    expect(jsonLines("not json")).toEqual(["not json"]);
  });
});

describe("rangeStart", () => {
  it("goes back the given number of days, or not at all", () => {
    expect(rangeStart(30, new Date("2026-10-31T00:00:00Z"))).toBe("2026-10-01T00:00:00.000Z");
    expect(rangeStart(null)).toBeUndefined();
  });
});
