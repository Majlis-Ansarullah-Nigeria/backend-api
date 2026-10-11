import { Bell, Building2, Calendar, FileText, History, Home, Inbox, LayoutTemplate, Network, User, Users, type LucideIcon } from "lucide-react";

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
  | "inbox";

export interface NavItem {
  href: string;
  key: NavItemKey;
  icon: LucideIcon;
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

/**
 * Secretariat navigation. The full tree from the mockup (Overview, Inbox, Report types,
 * Windows, Organization, Users, Dashboards, Exports, Announcements, Audit log, Settings)
 * lands incrementally as each of those routes is built — Report types (M3.1), Windows (M4.5),
 * Organization (M2.2), Users (M2.4) and Inbox (M7.1) exist so far. No mobile board exists for
 * these screens, so AppShell hides the bottom tabs here.
 */
export const SECRETARIAT_NAV_ITEMS: NavItem[] = [
  { href: "/secretariat/inbox", key: "inbox", icon: Inbox },
  { href: "/secretariat/report-types", key: "reportTypes", icon: LayoutTemplate },
  { href: "/secretariat/windows", key: "windows", icon: Calendar },
  { href: "/secretariat/organization", key: "organization", icon: Network },
  { href: "/secretariat/users", key: "users", icon: Users },
];
