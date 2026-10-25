import { describe, expect, it } from "vitest";
import { ROLE_NAME } from "@/features/admin/utils/role-name";
import type { GlobalSearchResults, SearchReportHit } from "@/lib/api/generated/hRSApiV1.schemas";
import { availableActions, matchesQuery, paletteItems, paletteRoles, reportHref, SEARCH_REPORT_ACCESS, unitHref } from "./command-palette";

const allOn = () => true;

const REPORT: SearchReportHit = {
  instanceId: "r1",
  unitId: "u1",
  unitName: "Agodi Muqam",
  reportTypeName: "General Monthly Report",
  periodLabel: "September 2026",
  referenceNumber: "GMR-2026-09-Z03D12M04",
  status: 2,
  access: SEARCH_REPORT_ACCESS.Review,
};

describe("availableActions", () => {
  it("offers the Secretariat its actions, including Create report type, but not a head's", () => {
    const keys = availableActions(paletteRoles([ROLE_NAME.Secretariat]), allOn).map((a) => a.key);
    expect(keys).toEqual(expect.arrayContaining(["openInbox", "createReportType", "createWindow", "goUsers", "goSettings"]));
    expect(keys).not.toContain("goMyReports");
    expect(keys).not.toContain("goWebhooks");
  });

  it("offers a head their own screens and never the Secretariat's", () => {
    const keys = availableActions(paletteRoles([ROLE_NAME.UnitHead]), allOn).map((a) => a.key);
    expect(keys).toEqual(["goMyReports", "goHistory", "goUnits", "goCompare", "goNotifications", "goProfile"]);
  });

  it("offers webhooks to the system administrator only", () => {
    expect(availableActions(paletteRoles([ROLE_NAME.SysAdmin]), allOn).map((a) => a.key)).toContain("goWebhooks");
  });

  it("drops actions for switched-off modules", () => {
    const keys = availableActions(paletteRoles([ROLE_NAME.Leadership]), (module) => module !== 1).map((a) => a.key);
    expect(keys).toContain("goDashboards");
    expect(keys).not.toContain("goCompiled");
  });
});

describe("matchesQuery", () => {
  it("needs every word, in any order, ignoring case", () => {
    expect(matchesQuery("Create report type", "type CREATE")).toBe(true);
    expect(matchesQuery("Create report type", "create window")).toBe(false);
  });
});

describe("links", () => {
  const head = paletteRoles([ROLE_NAME.UnitHead]);
  const secretariat = paletteRoles([ROLE_NAME.Secretariat]);

  it("sends each report to the screen the caller may use", () => {
    expect(reportHref(REPORT)).toBe("/secretariat/inbox?report=r1");
    expect(reportHref({ ...REPORT, access: SEARCH_REPORT_ACCESS.Own })).toBe("/reports/r1/edit?unit=u1");
    expect(reportHref({ ...REPORT, access: SEARCH_REPORT_ACCESS.Observed })).toBe("/units/u1/reports/r1");
  });

  it("opens a unit in the organization tree for admins, and on the head's own screens otherwise", () => {
    const unit = { id: "u1", name: "Zone 3", code: "Z03", levelName: "Zone", isObserved: false };
    expect(unitHref(unit, secretariat)).toBe("/secretariat/organization?unit=u1");
    expect(unitHref(unit, head)).toBe("/reports");
    expect(unitHref({ ...unit, isObserved: true }, head)).toBe("/units");
  });
});

describe("paletteItems", () => {
  const roles = paletteRoles([ROLE_NAME.Secretariat]);
  const actions = availableActions(roles, allOn).map((action) => ({ action, label: action.key }));
  const results: GlobalSearchResults = {
    units: [{ id: "u1", name: "Zone 3", code: "Z03", levelName: "Zone", isObserved: false }],
    users: [{ id: "p1", name: "Amina Bello", email: "amina@example.org" }],
    reports: [REPORT],
    reportTypes: [{ id: "t1", name: "General Monthly Report" }],
  };

  it("lists actions alone until there's a query of two characters", () => {
    expect(paletteItems(actions, "", results, roles).every((item) => item.kind === "action")).toBe(true);
    expect(paletteItems(actions, "z", results, roles).some((item) => item.kind !== "action")).toBe(false);
  });

  it("puts matching actions first, then reports, units, people and report types", () => {
    const items = paletteItems(actions, "goUsers", results, roles);
    expect(items.map((item) => item.kind)).toEqual(["action", "report", "unit", "user", "reportType"]);
    expect(items.find((item) => item.kind === "user")?.href).toBe("/secretariat/users?q=amina%40example.org");
    expect(items.find((item) => item.kind === "reportType")?.href).toBe("/secretariat/templates/t1");
  });
});
