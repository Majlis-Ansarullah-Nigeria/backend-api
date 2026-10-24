import { BarChart3, Bell, Building2, Calendar, Download, FileText, History, Home, Inbox, LayoutGrid, LayoutTemplate, Mail, Network, Settings, ShieldCheck, User, Users, type LucideIcon } from "lucide-react";

export type NavItemKey =
  | "home"
  | "reports"
  | "units"
  | "history"
  | "notifications"
  | "profile"
  | "organization"
  | "users"
  | "reportTypes"
  | "windows"
  | "inbox"
  | "announcements"
  | "exports"
  | "overview"
  | "dashboards"
  | "compiled"
  | "audit"
  | "settings";

export interface NavItem {
  href: string;
  key: NavItemKey;
  icon: LucideIcon;
  /** Active only on this exact path — for a section root like /secretariat whose sibling routes
   * all sit underneath it. */
  exact?: boolean;
}

export function isNavItemActive(item: NavItem, pathname: string | null): boolean {
  if (pathname === item.href) {
    return true;
  }
  return !item.exact && (pathname?.startsWith(`${item.href}/`) ?? false);
}

/**
 * Head/submitter navigation (matches the Head home and bottom-tab mockups).
 * Labels come from messages.Shell.nav via useTranslations, keyed by `key`.
 */
export const PRIMARY_NAV_ITEMS: NavItem[] = [
  { href: "/home", key: "home", icon: Home },
  { href: "/reports", key: "reports", icon: FileText },
  { href: "/units", key: "units", icon: Building2 },
  { href: "/history", key: "history", icon: History },
  { href: "/notifications", key: "notifications", icon: Bell },
  { href: "/profile", key: "profile", icon: User },
];

/** Added to the head navigation for people who have a dashboard (Zone/Dila heads, Leadership). */
export const DASHBOARD_NAV_ITEM: NavItem = { href: "/dashboards", key: "dashboards", icon: BarChart3 };

/** Added for Leadership, who read the compiled national reports (FR-REV-006). */
export const COMPILED_NAV_ITEM: NavItem = { href: "/compiled", key: "compiled", icon: FileText };

/**
 * Secretariat navigation. The full tree from the mockup (Overview, Inbox, Report types,
 * Windows, Organization, Users, Dashboards, Exports, Announcements, Audit log, Settings)
 * lands incrementally as each of those routes is built — Report types (M3.1), Windows (M4.5),
 * Organization (M2.2), Users (M2.4), Inbox (M7.1), Announcements/email wording (M8.1/M8.3),
 * Exports (M9.2), Overview and Dashboards (M10.1) and Audit log (M11.1) exist so far. No mobile board exists for these screens, so AppShell hides the
 * bottom tabs here.
 */
export const SECRETARIAT_NAV_ITEMS: NavItem[] = [
  { href: "/secretariat", key: "overview", icon: LayoutGrid, exact: true },
  { href: "/secretariat/inbox", key: "inbox", icon: Inbox },
  { href: "/secretariat/report-types", key: "reportTypes", icon: LayoutTemplate },
  { href: "/secretariat/windows", key: "windows", icon: Calendar },
  { href: "/secretariat/organization", key: "organization", icon: Network },
  { href: "/secretariat/users", key: "users", icon: Users },
  { href: "/secretariat/dashboards", key: "dashboards", icon: BarChart3 },
  { href: "/secretariat/exports", key: "exports", icon: Download },
  { href: "/secretariat/announcements", key: "announcements", icon: Mail },
  { href: "/secretariat/audit", key: "audit", icon: ShieldCheck },
  { href: "/admin/settings", key: "settings", icon: Settings },
];
