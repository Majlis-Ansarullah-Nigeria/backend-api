import { Bell, FileText, History, Home, Network, User, type LucideIcon } from "lucide-react";

export type NavItemKey = "home" | "reports" | "history" | "notifications" | "profile" | "organization";

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
  { href: "/history", key: "history", icon: History },
  { href: "/notifications", key: "notifications", icon: Bell },
  { href: "/profile", key: "profile", icon: User },
];

/**
 * Secretariat navigation. The full tree from the mockup (Overview, Inbox, Report types,
 * Windows, Organization, Users, Dashboards, Exports, Announcements, Audit log, Settings)
 * lands incrementally as each of those routes is built — only Organization (M2.2) exists
 * so far. No mobile board exists for these screens, so AppShell hides the bottom tabs here.
 */
export const SECRETARIAT_NAV_ITEMS: NavItem[] = [
  { href: "/secretariat/organization", key: "organization", icon: Network },
];
