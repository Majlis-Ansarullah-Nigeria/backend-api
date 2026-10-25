"use client";

import { Building2, CornerDownLeft, FileText, LayoutTemplate, Search, User, Zap } from "lucide-react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useRef, useState } from "react";
import { useFeatureFlags } from "@/features/admin/hooks/useFeatureFlags";
import type { GlobalSearchResults, MeProfile } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMe } from "@/lib/api/generated/identity/identity";
import { useGlobalSearch } from "@/lib/api/generated/search/search";
import { cn } from "@/lib/utils";
import { availableActions, paletteItems, paletteRoles, type PaletteItem } from "./command-palette";

export interface CommandPaletteProps {
  open: boolean;
  onClose: () => void;
}

/**
 * FR-SRC-001/002: the Ctrl/Cmd+K palette. Quick actions are filtered by role and switched-off
 * modules; search results come from GET /search, which only returns what the caller may open.
 * A native modal <dialog>: focus is trapped, Escape closes it and the page behind is inert.
 */
export function CommandPalette({ open, onClose }: CommandPaletteProps) {
  const t = useTranslations("Search");
  const router = useRouter();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [query, setQuery] = useState("");
  const [debounced, setDebounced] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);

  const me = useGetMe<MeProfile>({ query: { staleTime: 5 * 60 * 1000 } });
  const { isEnabled } = useFeatureFlags();
  const roles = useMemo(() => paletteRoles((me.data?.roleAssignments ?? []).map((a) => Number(a.role))), [me.data]);

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(query.trim()), 200);
    return () => clearTimeout(timeout);
  }, [query]);

  const search = useGlobalSearch<GlobalSearchResults>(
    { q: debounced },
    { query: { enabled: open && debounced.length >= 2, staleTime: 30 * 1000 } },
  );

  const actions = availableActions(roles, isEnabled).map((action) => ({ action, label: t(`actions.${action.key}`) }));
  const items = paletteItems(actions, query, query.trim() === debounced ? search.data : undefined, roles);
  const searching = query.trim().length >= 2 && (query.trim() !== debounced || search.isFetching);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }
    if (open && !dialog.open) {
      setQuery("");
      setDebounced("");
      setActiveIndex(0);
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  const go = (item: PaletteItem | undefined) => {
    if (!item) {
      return;
    }
    onClose();
    router.push(item.href);
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveIndex((index) => (items.length === 0 ? 0 : (index + 1) % items.length));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveIndex((index) => (items.length === 0 ? 0 : (index - 1 + items.length) % items.length));
    } else if (event.key === "Enter") {
      event.preventDefault();
      go(items[Math.min(activeIndex, items.length - 1)]);
    }
  };

  const activeId = items[activeIndex]?.id;
  const groups = groupItems(items);

  return (
    <dialog
      ref={dialogRef}
      aria-label={t("dialogLabel")}
      onClose={onClose}
      onClick={(event) => {
        // A click on the backdrop lands on the <dialog> itself.
        if (event.target === dialogRef.current) {
          onClose();
        }
      }}
      className="m-auto mt-[10vh] w-[calc(100%-2rem)] max-w-xl rounded-lg border border-line bg-surface p-0 text-ink shadow-xl backdrop:bg-forest-900/40"
    >
      <div className="flex items-center gap-2 border-b border-line px-4">
        <Search className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />
        <input
          type="text"
          role="combobox"
          aria-expanded="true"
          aria-controls="command-palette-list"
          aria-activedescendant={activeId}
          aria-autocomplete="list"
          aria-label={t("inputLabel")}
          placeholder={t("placeholder")}
          autoComplete="off"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value);
            setActiveIndex(0);
          }}
          onKeyDown={onKeyDown}
          className="h-14 flex-1 bg-transparent text-base text-ink outline-none placeholder:text-ink-muted"
        />
        <kbd className="rounded border border-line-strong bg-ground px-1.5 py-0.5 text-xs text-ink-muted">Esc</kbd>
      </div>

      <div id="command-palette-list" role="listbox" aria-label={t("resultsLabel")} className="max-h-[60vh] overflow-y-auto p-2">
        {groups.map((group) => (
          <div key={group.kind} role="group" aria-label={t(`groups.${group.kind}`)} className="mb-2 last:mb-0">
            <p className="px-2 py-1 text-xs font-semibold uppercase tracking-wide text-ink-muted" aria-hidden="true">
              {t(`groups.${group.kind}`)}
            </p>
            {group.items.map(({ item, index }) => (
              <div
                key={item.id}
                id={item.id}
                role="option"
                aria-selected={index === activeIndex}
                onMouseMove={() => setActiveIndex(index)}
                onClick={() => go(item)}
                className={cn(
                  "flex min-h-11 cursor-pointer items-center gap-3 rounded-md px-2 py-2 text-sm",
                  index === activeIndex ? "bg-mint text-ink" : "text-ink",
                )}
              >
                <ItemIcon item={item} />
                <ItemLabel item={item} />
                {index === activeIndex ? <CornerDownLeft className="ml-auto size-4 shrink-0 text-ink-muted" aria-hidden="true" /> : null}
              </div>
            ))}
          </div>
        ))}
        <p role="status" className="px-2 py-3 text-sm text-ink-muted empty:hidden">
          {searching ? t("searching") : items.length === 0 ? t("noResults") : ""}
        </p>
      </div>
    </dialog>
  );
}

const GROUP_ORDER = ["action", "report", "unit", "user", "reportType"] as const;

function groupItems(items: PaletteItem[]) {
  return GROUP_ORDER.map((kind) => ({
    kind,
    items: items.map((item, index) => ({ item, index })).filter(({ item }) => item.kind === kind),
  })).filter((group) => group.items.length > 0);
}

function ItemIcon({ item }: { item: PaletteItem }) {
  const Icon =
    item.kind === "action" ? Zap : item.kind === "report" ? FileText : item.kind === "unit" ? Building2 : item.kind === "user" ? User : LayoutTemplate;
  return <Icon className="size-4 shrink-0 text-green-700" aria-hidden="true" />;
}

function ItemLabel({ item }: { item: PaletteItem }) {
  const t = useTranslations("Search");
  switch (item.kind) {
    case "action":
      return <span className="font-medium">{t(`actions.${item.key}`)}</span>;
    case "report":
      return (
        <span className="min-w-0">
          <span className="block truncate font-medium">{t("reportLabel", { reportType: item.hit.reportTypeName, period: item.hit.periodLabel })}</span>
          <span className="block truncate text-xs text-ink-muted">
            {item.hit.unitName}
            {item.hit.referenceNumber ? ` · ${item.hit.referenceNumber}` : ""}
          </span>
        </span>
      );
    case "unit":
      return (
        <span className="min-w-0">
          <span className="block truncate font-medium">{t("unitLabel", { unit: item.hit.name })}</span>
          <span className="block truncate text-xs text-ink-muted">
            {item.hit.levelName} · {item.hit.code}
          </span>
        </span>
      );
    case "user":
      return (
        <span className="min-w-0">
          <span className="block truncate font-medium">{item.name}</span>
          <span className="block truncate text-xs text-ink-muted">{item.email}</span>
        </span>
      );
    default:
      return <span className="truncate font-medium">{t("reportTypeLabel", { reportType: item.name })}</span>;
  }
}
