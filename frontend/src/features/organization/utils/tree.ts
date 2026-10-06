import type { UnitLevelSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";

// Matches backend/src/HRS.Domain/Organization/UnitLevelCode.cs's enum order — the API
// serializes it as a plain number, so the frontend mirrors the declaration order here.
export const UNIT_LEVEL_CODE = {
  National: 0,
  Zone: 1,
  Dila: 2,
  Muqam: 3,
  Department: 4,
} as const;

export type OrgFilterTab = "all" | "vacant" | "archived";

export function buildChildrenMap(nodes: UnitTreeNode[]): Map<string | null, UnitTreeNode[]> {
  const map = new Map<string | null, UnitTreeNode[]>();
  for (const node of nodes) {
    const key = node.parentId ?? null;
    const siblings = map.get(key);
    if (siblings) {
      siblings.push(node);
    } else {
      map.set(key, [node]);
    }
  }
  return map;
}

/** Ids that should render: tab/search matches, plus every ancestor of a match (so hierarchy context survives a search). */
export function filterVisibleIds(nodes: UnitTreeNode[], tab: OrgFilterTab, search: string): Set<string> {
  const byId = new Map(nodes.map((node) => [node.id, node]));
  const needle = search.trim().toLowerCase();

  const matchesTab = (node: UnitTreeNode) => {
    if (tab === "archived") {
      return node.archivedAt !== null;
    }
    if (tab === "vacant") {
      return node.archivedAt === null && node.hasVacantHead;
    }
    return node.archivedAt === null;
  };

  const matchesSearch = (node: UnitTreeNode) =>
    needle === "" ||
    node.name.toLowerCase().includes(needle) ||
    node.code.toLowerCase().includes(needle) ||
    node.displayCode.toLowerCase().includes(needle) ||
    (node.headName?.toLowerCase().includes(needle) ?? false);

  const visible = new Set<string>();

  for (const node of nodes) {
    if (!matchesTab(node) || !matchesSearch(node)) {
      continue;
    }

    let current: UnitTreeNode | undefined = node;
    while (current) {
      visible.add(current.id);
      current = current.parentId ? byId.get(current.parentId) : undefined;
    }
  }

  return visible;
}

/** Root-to-node chain, for the detail panel's breadcrumb. */
export function ancestorChain(nodeId: string, nodes: UnitTreeNode[]): UnitTreeNode[] {
  const byId = new Map(nodes.map((node) => [node.id, node]));
  const chain: UnitTreeNode[] = [];
  let current = byId.get(nodeId);

  while (current) {
    chain.unshift(current);
    current = current.parentId ? byId.get(current.parentId) : undefined;
  }

  return chain;
}

/** "{n} zones · {n} dilaat · ..." summary line, one entry per level below National, in depth order. */
export function countsByLevel(
  nodes: UnitTreeNode[],
  levels: UnitLevelSummary[],
): { code: number; label: string; count: number }[] {
  return levels
    .filter((level) => level.code !== UNIT_LEVEL_CODE.National)
    .sort((a, b) => Number(a.depth) - Number(b.depth))
    .map((level) => ({
      code: level.code,
      label: level.pluralName.toLowerCase(),
      count: nodes.filter((node) => node.levelCode === level.code && node.archivedAt === null).length,
    }));
}

/** "{n} dilaat · {n} depts" for one node's immediate (visible) children — may span more than one level only for National. */
export function childCountsLabel(children: UnitTreeNode[], pluralByCode: Record<number, string>): string {
  const counts = new Map<number, number>();
  for (const child of children) {
    counts.set(child.levelCode, (counts.get(child.levelCode) ?? 0) + 1);
  }

  return [...counts.entries()]
    .map(([code, count]) => `${count} ${(pluralByCode[code] ?? "").toLowerCase()}`)
    .join(" · ");
}
