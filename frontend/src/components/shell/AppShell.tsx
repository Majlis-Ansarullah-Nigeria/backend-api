"use client";

import { useTranslations } from "next-intl";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useNotificationBadge } from "@/features/notifications/hooks/useNotificationBadge";
import { BottomTabs } from "./BottomTabs";
import { isNavItemActive, PRIMARY_NAV_ITEMS, SECRETARIAT_NAV_ITEMS } from "./nav-items";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import type { UnitOption } from "./UnitSwitcher";

export interface AppShellProps {
  children: ReactNode;
  user: { name: string; initials: string };
  unit: UnitOption;
  orgName?: string;
}

export function AppShell({ children, user, unit, orgName }: AppShellProps) {
  const pathname = usePathname();
  const t = useTranslations("Shell.nav");
  const notificationCount = useNotificationBadge();

  const isSecretariat = pathname?.startsWith("/secretariat") ?? false;
  const navItems = isSecretariat ? SECRETARIAT_NAV_ITEMS : PRIMARY_NAV_ITEMS;

  const activeItem = navItems.find((item) => isNavItemActive(item, pathname));

  return (
    <div className="flex min-h-dvh">
      <Sidebar orgName={orgName} user={user} unit={unit} navItems={navItems} />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          title={activeItem ? t(activeItem.key) : "HRS"}
          user={user}
          notificationCount={notificationCount}
        />
        <main className="flex-1 overflow-y-auto pb-20 md:pb-0">{children}</main>
      </div>
      {isSecretariat ? null : <BottomTabs />}
    </div>
  );
}
