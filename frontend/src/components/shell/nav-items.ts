import { Bell, FileText, History, Home, User, type LucideIcon } from "lucide-react";

export type NavItemKey = "home" | "reports" | "history" | "notifications" | "profile";

export interface NavItem {
  href: string;
  key: NavItemKey;
  icon: LucideIcon;
}

/**
 * Head/submitter navigation (matches the Head home and bottom-tab mockups).
 * Labels come from messages.Shell.nav via useTranslations, keyed by `key`.
 * Secretariat/Department nav sets are a different, larger tree tied to
 * RoleAssignment (M1) — they land with the Secretariat routes (M7+).
 */
export const PRIMARY_NAV_ITEMS: NavItem[] = [
  { href: "/home", key: "home", icon: Home },
  { href: "/reports", key: "reports", icon: FileText },
  { href: "/history", key: "history", icon: History },
  { href: "/notifications", key: "notifications", icon: Bell },
  { href: "/profile", key: "profile", icon: User },
];
