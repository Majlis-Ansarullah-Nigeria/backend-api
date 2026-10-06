"use client";

import { Network, Plus, Search } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { UnitLevelSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetOrganizationTree, useGetUnitLevels } from "@/lib/api/generated/organization/organization";
import { cn } from "@/lib/utils";
import {
  ancestorChain,
  buildChildrenMap,
  countsByLevel,
  filterVisibleIds,
  type OrgFilterTab,
} from "@/features/organization/utils/tree";
import { AddUnitPanel } from "./AddUnitPanel";
import { OrganizationTree } from "./OrganizationTree";
import { UnitDetailPanel } from "./UnitDetailPanel";

function toggleSet(set: Set<string>, id: string): Set<string> {
  const next = new Set(set);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  return next;
}

export function OrganizationScreen() {
  const t = useTranslations("Organization");
  const tree = useGetOrganizationTree<UnitTreeNode[], ApiError>({ includeArchived: true });
  const levels = useGetUnitLevels<UnitLevelSummary[], ApiError>();

  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<OrgFilterTab>("all");
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const didAutoExpand = useRef(false);

  const nodes = useMemo(() => tree.data ?? [], [tree.data]);
  const levelList = useMemo(() => levels.data ?? [], [levels.data]);

  const childrenMap = useMemo(() => buildChildrenMap(nodes), [nodes]);
  const roots = useMemo(() => childrenMap.get(null) ?? [], [childrenMap]);
  const visibleIds = useMemo(() => filterVisibleIds(nodes, tab, search), [nodes, tab, search]);

  const expandedForRender = useMemo(() => {
    if (search.trim() === "") {
      return expandedIds;
    }
    // Searching auto-opens every ancestor of a match, on top of whatever the user expanded by hand.
    return new Set([...expandedIds, ...visibleIds]);
  }, [search, expandedIds, visibleIds]);

  useEffect(() => {
    if (!didAutoExpand.current && roots.length > 0) {
      didAutoExpand.current = true;
      setExpandedIds(new Set(roots.map((root) => root.id)));
    }
  }, [roots]);

  const levelNameByCode = useMemo(() => {
    const map: Record<number, string> = {};
    for (const level of levelList) {
      map[level.code] = level.name;
    }
    return map;
  }, [levelList]);

  const pluralByCode = useMemo(() => {
    const map: Record<number, string> = {};
    for (const level of levelList) {
      map[level.code] = level.pluralName;
    }
    return map;
  }, [levelList]);

  const summary = useMemo(() => countsByLevel(nodes, levelList), [nodes, levelList]);
  const vacantCount = nodes.filter((node) => node.archivedAt === null && node.hasVacantHead).length;
  const archivedCount = nodes.filter((node) => node.archivedAt !== null).length;

  const selectedNode = nodes.find((node) => node.id === selectedId) ?? null;
  const breadcrumb = selectedNode ? ancestorChain(selectedNode.id, nodes) : [];

  const selectUnit = (id: string) => {
    setSelectedId(id);
    setIsAdding(false);
  };

  if (tree.isPending || levels.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (tree.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("treeErrorTitle")} description={t("treeErrorDescription")} onRetry={() => tree.refetch()} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{summary.map((entry) => `${entry.count} ${entry.label}`).join(" · ")}</p>
        </div>
        <Button
          onClick={() => {
            setIsAdding(true);
            setSelectedId(null);
          }}
        >
          <Plus aria-hidden="true" />
          {t("addUnit")}
        </Button>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex w-full max-w-md items-center gap-2 rounded-md border border-line-strong bg-surface px-3 py-2">
          <Search className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("searchPlaceholder")}
            aria-label={t("searchPlaceholder")}
            className="w-full bg-transparent text-sm text-ink outline-none placeholder:text-ink-muted"
          />
        </div>

        <div className="flex gap-1" role="tablist" aria-label={t("filterLabel")}>
          <FilterTab active={tab === "all"} onClick={() => setTab("all")} label={t("filterAll")} />
          <FilterTab active={tab === "vacant"} onClick={() => setTab("vacant")} label={t("filterVacant", { count: vacantCount })} />
          <FilterTab active={tab === "archived"} onClick={() => setTab("archived")} label={t("filterArchived", { count: archivedCount })} />
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <div className="rounded-lg border border-line bg-surface p-2">
          {roots.length === 0 ? (
            <EmptyState icon={Network} title={t("emptyTitle")} description={t("emptyDescription")} />
          ) : (
            <OrganizationTree
              roots={roots}
              childrenMap={childrenMap}
              visibleIds={visibleIds}
              expandedIds={expandedForRender}
              selectedId={selectedId}
              levelNameByCode={levelNameByCode}
              pluralByCode={pluralByCode}
              onToggleExpand={(id) => setExpandedIds((prev) => toggleSet(prev, id))}
              onSelect={selectUnit}
            />
          )}
        </div>

        <div>
          {isAdding ? (
            <AddUnitPanel
              levels={levelList}
              units={nodes}
              defaultParentId={selectedId}
              onCancel={() => setIsAdding(false)}
              onCreated={(id) => {
                setIsAdding(false);
                selectUnit(id);
              }}
            />
          ) : selectedNode ? (
            <UnitDetailPanel unitId={selectedNode.id} breadcrumb={breadcrumb} levelName={levelNameByCode[selectedNode.levelCode]} />
          ) : (
            <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("selectPrompt")}</div>
          )}
        </div>
      </div>
    </div>
  );
}

function FilterTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "min-h-9 rounded-md px-3 text-sm font-medium transition-colors",
        active ? "bg-mint text-green-700" : "text-ink-muted hover:bg-ground",
      )}
    >
      {label}
    </button>
  );
}
