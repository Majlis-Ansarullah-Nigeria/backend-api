"use client";

import { Search, Users as UsersIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import { useMemo, useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import type { ApiError } from "@/lib/api/client";
import type { UnitTreeNode, UserListItem } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetUsers } from "@/lib/api/generated/admin/admin";
import { useGetOrganizationTree } from "@/lib/api/generated/organization/organization";
import { roleLabel } from "@/features/admin/utils/role-name";
import { USER_STATUS, filterUsersByTab, searchUsers, statusLabel, type UserStatusTab } from "@/features/admin/utils/users";
import { cn } from "@/lib/utils";
import { UserDetailPanel } from "./UserDetailPanel";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

const STATUS_BADGE_STYLES: Record<number, string> = {
  [USER_STATUS.Active]: "bg-status-acknowledged-bg text-status-acknowledged-fg",
  [USER_STATUS.Invited]: "bg-status-submitted-bg text-status-submitted-fg",
  [USER_STATUS.Deactivated]: "bg-status-missed-bg text-status-missed-fg",
};

/** `initialSearch` (from `?q=`, the search palette) pre-fills the search box. */
export function UsersScreen({ initialSearch }: { initialSearch?: string } = {}) {
  const fmt = useOrgFormat();
  const t = useTranslations("AdminUsers");
  const users = useGetUsers<UserListItem[], ApiError>();
  const tree = useGetOrganizationTree<UnitTreeNode[], ApiError>({ includeArchived: true });

  const [search, setSearch] = useState(initialSearch ?? "");
  const [tab, setTab] = useState<UserStatusTab>("all");
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const allUsers = useMemo(() => users.data ?? [], [users.data]);
  const visibleUsers = useMemo(() => searchUsers(filterUsersByTab(allUsers, tab), search), [allUsers, tab, search]);

  const activeCount = allUsers.filter((u) => u.status === USER_STATUS.Active).length;
  const invitedCount = allUsers.filter((u) => u.status === USER_STATUS.Invited).length;
  const deactivatedCount = allUsers.filter((u) => u.status === USER_STATUS.Deactivated).length;

  const keyOf = (u: UserListItem) => u.userId ?? `invite:${u.invitationId}`;
  const selected = allUsers.find((u) => keyOf(u) === selectedKey) ?? null;

  if (users.isPending || tree.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (users.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => users.refetch()} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">
          {t("summary", { count: allUsers.length, invited: invitedCount })}
        </p>
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
          <FilterTab active={tab === "all"} onClick={() => setTab("all")} label={t("tabAll", { count: allUsers.length })} />
          <FilterTab active={tab === "active"} onClick={() => setTab("active")} label={t("tabActive", { count: activeCount })} />
          <FilterTab active={tab === "invited"} onClick={() => setTab("invited")} label={t("tabInvited", { count: invitedCount })} />
          <FilterTab active={tab === "deactivated"} onClick={() => setTab("deactivated")} label={t("tabDeactivated", { count: deactivatedCount })} />
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <div className="overflow-x-auto rounded-lg border border-line bg-surface">
          {visibleUsers.length === 0 ? (
            <EmptyState icon={UsersIcon} title={t("emptyTitle")} description={t("emptyDescription")} />
          ) : (
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="bg-ground text-xs uppercase text-ink-muted">
                <tr>
                  <th className="px-3 py-2">{t("columnPerson")}</th>
                  <th className="px-3 py-2">{t("columnRole")}</th>
                  <th className="px-3 py-2">{t("columnLastSignIn")}</th>
                  <th className="px-3 py-2">{t("columnStatus")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {visibleUsers.map((user) => {
                  const key = keyOf(user);
                  return (
                    <tr
                      key={key}
                      onClick={() => setSelectedKey(key)}
                      className={cn("cursor-pointer hover:bg-ground", selectedKey === key && "bg-mint")}
                    >
                      <td className="px-3 py-2.5">
                        <p className="font-medium text-ink">{user.name}</p>
                        <p className="text-xs text-ink-muted">{user.email}</p>
                      </td>
                      <td className="px-3 py-2.5 text-ink-muted">
                        {user.roleAssignments.length > 0
                          ? user.roleAssignments.map((a) => roleLabel(a.role)).join(" + ")
                          : "—"}
                      </td>
                      <td className="px-3 py-2.5 text-ink-muted">
                        {user.lastLoginAt ? fmt.dateTime(user.lastLoginAt) : t("never")}
                      </td>
                      <td className="px-3 py-2.5">
                        <span className={cn("inline-flex rounded-full px-2 py-0.5 text-xs font-medium", STATUS_BADGE_STYLES[user.status])}>
                          {statusLabel(user.status)}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        <div>
          {selected ? (
            <UserDetailPanel user={selected} units={tree.data ?? []} />
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
