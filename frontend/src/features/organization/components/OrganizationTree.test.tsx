import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import type { UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { buildChildrenMap } from "@/features/organization/utils/tree";
import { OrganizationTree } from "./OrganizationTree";

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

const NATIONAL = node({ id: "national", levelCode: 0, name: "National HQ", headName: "M. Okafor", hasVacantHead: false });
const ZONE = node({ id: "zone", levelCode: 1, parentId: "national", name: "Lagos Zone", headName: "O. Balogun", hasVacantHead: false });
const MUQAM_VACANT = node({ id: "muqam-vacant", levelCode: 3, parentId: "zone", name: "Iwo Road Muqam" });

const ALL_NODES = [NATIONAL, ZONE, MUQAM_VACANT];
const LEVEL_NAMES = { 0: "National", 1: "Zone", 3: "Muqam" };
const PLURALS = { 1: "Zones", 3: "Muqamaat" };

function renderTree(overrides: Partial<Parameters<typeof OrganizationTree>[0]> = {}) {
  const childrenMap = buildChildrenMap(ALL_NODES);
  const onSelect = vi.fn();
  const onToggleExpand = vi.fn();

  renderWithIntl(
    <OrganizationTree
      roots={[NATIONAL]}
      childrenMap={childrenMap}
      visibleIds={new Set(ALL_NODES.map((n) => n.id))}
      expandedIds={new Set()}
      selectedId={null}
      levelNameByCode={LEVEL_NAMES}
      pluralByCode={PLURALS}
      onToggleExpand={onToggleExpand}
      onSelect={onSelect}
      {...overrides}
    />,
  );

  return { onSelect, onToggleExpand };
}

describe("OrganizationTree", () => {
  it("shows a root node but hides its children when collapsed", () => {
    renderTree({ expandedIds: new Set() });

    expect(screen.getByText("National HQ")).toBeInTheDocument();
    expect(screen.queryByText("Lagos Zone")).not.toBeInTheDocument();
  });

  it("shows children once a node is expanded", () => {
    renderTree({ expandedIds: new Set(["national"]) });

    expect(screen.getByText("Lagos Zone")).toBeInTheDocument();
  });

  it("shows the head's name for a filled unit and a vacant badge for an empty one", () => {
    renderTree({ expandedIds: new Set(["national", "zone"]) });

    expect(screen.getByText("M. Okafor")).toBeInTheDocument();
    expect(screen.getByText("O. Balogun")).toBeInTheDocument();
    expect(screen.getByText("Vacant head")).toBeInTheDocument();
  });

  it("calls onToggleExpand when the chevron is clicked", async () => {
    const user = userEvent.setup();
    const { onToggleExpand } = renderTree({ expandedIds: new Set() });

    await user.click(screen.getByRole("button", { name: /expand national/i }));

    expect(onToggleExpand).toHaveBeenCalledWith("national");
  });

  it("calls onSelect when a row is clicked", async () => {
    const user = userEvent.setup();
    const { onSelect } = renderTree({ expandedIds: new Set() });

    await user.click(screen.getByText("National HQ"));

    expect(onSelect).toHaveBeenCalledWith("national");
  });

  it("respects visibleIds, hiding nodes that don't match the current filter/search", () => {
    renderTree({
      expandedIds: new Set(["national", "zone"]),
      visibleIds: new Set(["national", "zone"]),
    });

    expect(screen.getByText("Lagos Zone")).toBeInTheDocument();
    expect(screen.queryByText("Iwo Road Muqam")).not.toBeInTheDocument();
  });
});
