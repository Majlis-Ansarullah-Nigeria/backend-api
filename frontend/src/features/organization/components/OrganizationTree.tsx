"use client";

import { ChevronDown, ChevronRight } from "lucide-react";
import { useTranslations } from "next-intl";
import type { UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { cn } from "@/lib/utils";
import { childCountsLabel } from "@/features/organization/utils/tree";
import { LevelBadge, VacantHeadBadge } from "./badges";

interface TreeRowSharedProps {
  childrenMap: Map<string | null, UnitTreeNode[]>;
  visibleIds: Set<string>;
  expandedIds: Set<string>;
  selectedId: string | null;
  levelNameByCode: Record<number, string>;
  pluralByCode: Record<number, string>;
  onToggleExpand: (id: string) => void;
  onSelect: (id: string) => void;
}

export interface OrganizationTreeProps extends TreeRowSharedProps {
  roots: UnitTreeNode[];
}

export function OrganizationTree({ roots, ...shared }: OrganizationTreeProps) {
  return (
    <ul role="tree" aria-label="Organization">
      {roots
        .filter((root) => shared.visibleIds.has(root.id))
        .map((root) => (
          <UnitRow key={root.id} node={root} depth={0} {...shared} />
        ))}
    </ul>
  );
}

function UnitRow({
  node,
  depth,
  childrenMap,
  visibleIds,
  expandedIds,
  selectedId,
  levelNameByCode,
  pluralByCode,
  onToggleExpand,
  onSelect,
}: TreeRowSharedProps & { node: UnitTreeNode; depth: number }) {
  const t = useTranslations("Organization");
  const allChildren = childrenMap.get(node.id) ?? [];
  const children = allChildren.filter((child) => visibleIds.has(child.id));
  const hasChildren = children.length > 0;
  const expanded = expandedIds.has(node.id);
  const selected = selectedId === node.id;

  return (
    <li role="treeitem" aria-expanded={hasChildren ? expanded : undefined} aria-selected={selected}>
      <div
        className={cn("flex min-h-11 items-center gap-1 rounded-md pr-2", selected && "bg-mint")}
        style={{ paddingLeft: `${depth * 20}px` }}
      >
        {hasChildren ? (
          <button
            type="button"
            aria-label={expanded ? t("collapse", { name: node.name }) : t("expand", { name: node.name })}
            onClick={() => onToggleExpand(node.id)}
            className="flex size-7 shrink-0 items-center justify-center text-ink-muted hover:text-ink"
          >
            {expanded ? <ChevronDown className="size-4" aria-hidden="true" /> : <ChevronRight className="size-4" aria-hidden="true" />}
          </button>
        ) : (
          <span className="size-7 shrink-0" aria-hidden="true" />
        )}

        <button
          type="button"
          onClick={() => onSelect(node.id)}
          className="flex min-h-11 min-w-0 flex-1 items-center justify-between gap-3 rounded-md px-2 py-1.5 text-left hover:bg-ground"
        >
          <span className="flex min-w-0 items-center gap-2">
            <LevelBadge code={node.levelCode} label={levelNameByCode[node.levelCode] ?? ""} />
            <span className="truncate text-sm font-medium text-ink">{node.name}</span>
          </span>

          <span className="flex shrink-0 items-center gap-2 text-xs text-ink-muted sm:text-sm">
            {hasChildren ? (
              <span className="hidden whitespace-nowrap sm:inline">{childCountsLabel(allChildren, pluralByCode)}</span>
            ) : null}
            {node.hasVacantHead ? (
              <VacantHeadBadge label={t("vacantHead")} />
            ) : node.headName ? (
              <span className="truncate text-ink">{node.headName}</span>
            ) : null}
          </span>
        </button>
      </div>

      {hasChildren && expanded ? (
        <ul role="group">
          {children.map((child) => (
            <UnitRow
              key={child.id}
              node={child}
              depth={depth + 1}
              childrenMap={childrenMap}
              visibleIds={visibleIds}
              expandedIds={expandedIds}
              selectedId={selectedId}
              levelNameByCode={levelNameByCode}
              pluralByCode={pluralByCode}
              onToggleExpand={onToggleExpand}
              onSelect={onSelect}
            />
          ))}
        </ul>
      ) : null}
    </li>
  );
}
