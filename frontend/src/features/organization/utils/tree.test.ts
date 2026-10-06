import { describe, expect, it } from "vitest";
import type { UnitLevelSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { ancestorChain, buildChildrenMap, childCountsLabel, countsByLevel, filterVisibleIds } from "./tree";

function node(overrides: Partial<UnitTreeNode> & { id: string }): UnitTreeNode {
  return {
    code: overrides.id.toUpperCase(),
    displayCode: overrides.id.toUpperCase(),
    name: overrides.id,
    levelId: "level",
    levelCode: 1,
    parentId: null,
    path: overrides.id,
    archivedAt: null,
    headName: null,
    hasVacantHead: true,
    ...overrides,
  };
}

const NATIONAL = node({ id: "national", levelCode: 0, parentId: null, headName: "M. Okafor", hasVacantHead: false });
const ZONE = node({ id: "zone", levelCode: 1, parentId: "national", name: "Lagos Zone", headName: "O. Balogun", hasVacantHead: false });
const DILA = node({ id: "dila", levelCode: 2, parentId: "zone", name: "Ibadan Dila", headName: "K. Bello", hasVacantHead: false });
const MUQAM_VACANT = node({ id: "muqam-vacant", levelCode: 3, parentId: "dila", name: "Iwo Road Muqam", hasVacantHead: true });
const MUQAM_FILLED = node({ id: "muqam-filled", levelCode: 3, parentId: "dila", name: "Agodi Muqam", headName: "A. Adeyemi", hasVacantHead: false });
const ARCHIVED_DILA = node({ id: "archived-dila", levelCode: 2, parentId: "zone", name: "Osogbo Dila", archivedAt: "2026-01-01T00:00:00Z" });

const ALL_NODES = [NATIONAL, ZONE, DILA, MUQAM_VACANT, MUQAM_FILLED, ARCHIVED_DILA];

describe("buildChildrenMap", () => {
  it("groups nodes by parentId, with null-parent roots under the null key", () => {
    const map = buildChildrenMap(ALL_NODES);

    expect(map.get(null)).toEqual([NATIONAL]);
    expect(map.get("zone")).toEqual([DILA, ARCHIVED_DILA]);
    expect(map.get("dila")).toEqual([MUQAM_VACANT, MUQAM_FILLED]);
  });
});

describe("filterVisibleIds", () => {
  it("'all' hides archived units but keeps the rest", () => {
    const visible = filterVisibleIds(ALL_NODES, "all", "");

    expect(visible.has("archived-dila")).toBe(false);
    expect(visible.has("muqam-vacant")).toBe(true);
  });

  it("'vacant' keeps only vacant-head units, plus their ancestors", () => {
    const visible = filterVisibleIds(ALL_NODES, "vacant", "");

    expect(visible).toEqual(new Set(["national", "zone", "dila", "muqam-vacant"]));
  });

  it("'archived' shows only archived units and their ancestor chain", () => {
    const visible = filterVisibleIds(ALL_NODES, "archived", "");

    expect(visible).toEqual(new Set(["national", "zone", "archived-dila"]));
  });

  it("search matches name case-insensitively and keeps ancestors visible", () => {
    const visible = filterVisibleIds(ALL_NODES, "all", "agodi");

    expect(visible).toEqual(new Set(["national", "zone", "dila", "muqam-filled"]));
  });

  it("search also matches the head's name", () => {
    const visible = filterVisibleIds(ALL_NODES, "all", "bello");

    expect(visible.has("dila")).toBe(true);
    expect(visible.has("muqam-filled")).toBe(false);
  });

  it("empty search with 'all' returns every non-archived node", () => {
    const visible = filterVisibleIds(ALL_NODES, "all", "");

    expect(visible).toEqual(new Set(["national", "zone", "dila", "muqam-vacant", "muqam-filled"]));
  });
});

describe("ancestorChain", () => {
  it("returns the root-to-node path", () => {
    expect(ancestorChain("muqam-filled", ALL_NODES)).toEqual([NATIONAL, ZONE, DILA, MUQAM_FILLED]);
  });

  it("returns just the node itself for a root", () => {
    expect(ancestorChain("national", ALL_NODES)).toEqual([NATIONAL]);
  });
});

describe("countsByLevel", () => {
  const levels: UnitLevelSummary[] = [
    { id: "l0", code: 0, name: "National", pluralName: "National", depth: 0 },
    { id: "l1", code: 1, name: "Zone", pluralName: "Zones", depth: 1 },
    { id: "l2", code: 2, name: "Dila", pluralName: "Dilaat", depth: 2 },
    { id: "l3", code: 3, name: "Muqam", pluralName: "Muqamaat", depth: 3 },
  ];

  it("excludes National and counts non-archived units per level", () => {
    const counts = countsByLevel(ALL_NODES, levels);

    expect(counts).toEqual([
      { code: 1, label: "zones", count: 1 },
      { code: 2, label: "dilaat", count: 1 },
      { code: 3, label: "muqamaat", count: 2 },
    ]);
  });
});

describe("childCountsLabel", () => {
  it("joins one group per level present among the children", () => {
    const label = childCountsLabel([MUQAM_VACANT, MUQAM_FILLED], { 3: "Muqamaat" });

    expect(label).toBe("2 muqamaat");
  });

  it("splits into multiple groups when children span levels (e.g. National's zones + depts)", () => {
    const dept = node({ id: "dept", levelCode: 4, parentId: "national", name: "Finance" });
    const label = childCountsLabel([ZONE, dept], { 1: "Zones", 4: "Departments" });

    expect(label).toBe("1 zones · 1 departments");
  });
});
