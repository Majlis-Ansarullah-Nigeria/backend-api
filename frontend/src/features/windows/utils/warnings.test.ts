import { describe, expect, it } from "vitest";
import { UNIT_LEVEL_CODE } from "@/features/templates/utils/report-type";
import { detectParentBeforeChildWarnings } from "./warnings";

describe("detectParentBeforeChildWarnings", () => {
  it("warns when a parent level is due before its child", () => {
    const warnings = detectParentBeforeChildWarnings([
      { level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-11-05T23:59:00Z" },
      { level: UNIT_LEVEL_CODE.Dila, dueAt: "2026-11-03T23:59:00Z" },
    ]);

    expect(warnings).toHaveLength(1);
    expect(warnings[0]).toContain("Dila is due before Muqam");
  });

  it("does not warn when the parent is due after the child", () => {
    const warnings = detectParentBeforeChildWarnings([
      { level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-11-05T23:59:00Z" },
      { level: UNIT_LEVEL_CODE.Dila, dueAt: "2026-11-15T23:59:00Z" },
    ]);

    expect(warnings).toHaveLength(0);
  });

  it("skips a level pair when either side has no due date", () => {
    const warnings = detectParentBeforeChildWarnings([{ level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-11-05T23:59:00Z" }]);

    expect(warnings).toHaveLength(0);
  });

  it("can warn on more than one link in the chain at once", () => {
    const warnings = detectParentBeforeChildWarnings([
      { level: UNIT_LEVEL_CODE.Muqam, dueAt: "2026-11-10T23:59:00Z" },
      { level: UNIT_LEVEL_CODE.Dila, dueAt: "2026-11-03T23:59:00Z" },
      { level: UNIT_LEVEL_CODE.Zone, dueAt: "2026-11-01T23:59:00Z" },
    ]);

    expect(warnings).toHaveLength(2);
  });
});
