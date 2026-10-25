import { FEATURE_MODULE, type FeatureModuleValue } from "@/features/admin/hooks/useFeatureFlags";
import { ROLE_NAME } from "@/features/admin/utils/role-name";
import type { GlobalSearchResults, SearchReportHit, SearchUnitHit } from "@/lib/api/generated/hRSApiV1.schemas";

/** Mirrors the backend SearchReportAccess enum. */
export const SEARCH_REPORT_ACCESS = { Review: 0, Own: 1, Observed: 2 } as const;

export interface PaletteRoles {
  secretariat: boolean;
  sysAdmin: boolean;
  head: boolean;
  leadership: boolean;
}

export function paletteRoles(roles: number[]): PaletteRoles {
  return {
    secretariat: roles.includes(ROLE_NAME.Secretariat),
    sysAdmin: roles.includes(ROLE_NAME.SysAdmin),
    head: roles.includes(ROLE_NAME.UnitHead) || roles.includes(ROLE_NAME.Delegate),
    leadership: roles.includes(ROLE_NAME.Leadership),
  };
}

export type QuickActionKey =
  | "createReportType"
  | "createWindow"
  | "openInbox"
  | "sendAnnouncement"
  | "goOverview"
  | "goOrganization"
  | "goCompliance"
  | "goExports"
  | "goAudit"
  | "goUsers"
  | "goSettings"
  | "goWebhooks"
  | "goMyReports"
  | "goHistory"
  | "goUnits"
  | "goCompare"
  | "goDashboards"
  | "goCompiled"
  | "goNotifications"
  | "goProfile";

export interface QuickAction {
  key: QuickActionKey;
  href: string;
  /** Who it's offered to. Navigation only — every screen's API checks access itself. */
  visible: (roles: PaletteRoles) => boolean;
  feature?: FeatureModuleValue;
}

const orgAdmin = (r: PaletteRoles) => r.secretariat || r.sysAdmin;

/** FR-SRC-002's quick actions, in the order they're listed with an empty query. */
export const QUICK_ACTIONS: QuickAction[] = [
  { key: "openInbox", href: "/secretariat/inbox", visible: (r) => r.secretariat },
  { key: "createReportType", href: "/secretariat/report-types?new=1", visible: (r) => r.secretariat },
  { key: "createWindow", href: "/secretariat/windows/new", visible: (r) => r.secretariat },
  { key: "sendAnnouncement", href: "/secretariat/announcements", visible: (r) => r.secretariat },
  { key: "goMyReports", href: "/reports", visible: (r) => r.head },
  { key: "goHistory", href: "/history", visible: (r) => r.head },
  { key: "goUnits", href: "/units", visible: (r) => r.head },
  { key: "goCompare", href: "/history/compare", visible: (r) => r.head || r.secretariat, feature: FEATURE_MODULE.ComparisonAndVersions },
  { key: "goOverview", href: "/secretariat", visible: (r) => r.secretariat },
  { key: "goOrganization", href: "/secretariat/organization", visible: orgAdmin },
  { key: "goCompliance", href: "/secretariat/dashboards/compliance", visible: (r) => r.secretariat, feature: FEATURE_MODULE.Dashboards },
  { key: "goDashboards", href: "/dashboards", visible: (r) => r.leadership, feature: FEATURE_MODULE.Dashboards },
  { key: "goCompiled", href: "/compiled", visible: (r) => r.leadership, feature: FEATURE_MODULE.CompiledReports },
  { key: "goExports", href: "/secretariat/exports", visible: (r) => r.secretariat },
  { key: "goUsers", href: "/secretariat/users", visible: orgAdmin },
  { key: "goAudit", href: "/secretariat/audit", visible: orgAdmin },
  { key: "goSettings", href: "/admin/settings", visible: orgAdmin },
  { key: "goWebhooks", href: "/admin/webhooks", visible: (r) => r.sysAdmin },
  { key: "goNotifications", href: "/notifications", visible: () => true },
  { key: "goProfile", href: "/profile", visible: () => true },
];

export function availableActions(roles: PaletteRoles, isEnabled: (module: FeatureModuleValue) => boolean): QuickAction[] {
  return QUICK_ACTIONS.filter((action) => action.visible(roles) && (action.feature === undefined || isEnabled(action.feature)));
}

/** Every word of the query appears somewhere in the label — same rule as the server search. */
export function matchesQuery(label: string, query: string): boolean {
  const haystack = label.toLowerCase();
  return query
    .toLowerCase()
    .split(/\s+/)
    .filter(Boolean)
    .every((word) => haystack.includes(word));
}

export function unitHref(unit: SearchUnitHit, roles: PaletteRoles): string {
  if (orgAdmin(roles)) {
    return `/secretariat/organization?unit=${unit.id}`;
  }
  return unit.isObserved ? "/units" : "/reports";
}

export function reportHref(report: SearchReportHit): string {
  switch (Number(report.access)) {
    case SEARCH_REPORT_ACCESS.Review:
      return `/secretariat/inbox?report=${report.instanceId}`;
    case SEARCH_REPORT_ACCESS.Observed:
      return `/units/${report.unitId}/reports/${report.instanceId}`;
    default:
      return `/reports/${report.instanceId}/edit?unit=${report.unitId}`;
  }
}

export type PaletteItem =
  | { kind: "action"; id: string; href: string; key: QuickActionKey }
  | { kind: "unit"; id: string; href: string; hit: SearchUnitHit }
  | { kind: "report"; id: string; href: string; hit: SearchReportHit }
  | { kind: "user"; id: string; href: string; name: string; email: string }
  | { kind: "reportType"; id: string; href: string; name: string };

/** The flat, keyboard-navigable list: matching actions first, then each result group. */
export function paletteItems(
  actions: { action: QuickAction; label: string }[],
  query: string,
  results: GlobalSearchResults | undefined,
  roles: PaletteRoles,
): PaletteItem[] {
  const matching = query.trim() === "" ? actions.slice(0, 8) : actions.filter(({ label }) => matchesQuery(label, query)).slice(0, 5);
  const items: PaletteItem[] = matching.map(({ action }) => ({ kind: "action", id: `action-${action.key}`, href: action.href, key: action.key }));
  if (!results || query.trim().length < 2) {
    return items;
  }
  items.push(...results.reports.map((hit) => ({ kind: "report" as const, id: `report-${hit.instanceId}`, href: reportHref(hit), hit })));
  items.push(...results.units.map((hit) => ({ kind: "unit" as const, id: `unit-${hit.id}`, href: unitHref(hit, roles), hit })));
  items.push(
    ...results.users.map((user) => ({
      kind: "user" as const,
      id: `user-${user.id}`,
      href: `/secretariat/users?q=${encodeURIComponent(user.email)}`,
      name: user.name,
      email: user.email,
    })),
  );
  items.push(
    ...results.reportTypes.map((type) => ({
      kind: "reportType" as const,
      id: `type-${type.id}`,
      href: `/secretariat/templates/${type.id}`,
      name: type.name,
    })),
  );
  return items;
}
