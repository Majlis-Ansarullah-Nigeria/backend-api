"use client";

import { useTranslations } from "next-intl";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { BottomTabs } from "./BottomTabs";
import { PRIMARY_NAV_ITEMS } from "./nav-items";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import type { UnitOption } from "./UnitSwitcher";

export interface AppShellProps {
  children: ReactNode;
  user: { name: string; initials: string };
  unit: UnitOption;
  orgName?: string;
  notificationCount?: number;
}

export function AppShell({ children, user, unit, orgName, notificationCount }: AppShellProps) {
  const pathname = usePathname();
  const t = useTranslations("Shell.nav");

  const activeItem = PRIMARY_NAV_ITEMS.find(
    (item) => pathname === item.href || pathname?.startsWith(`${item.href}/`),
  );

  return (
    <div className="flex min-h-dvh">
      <Sidebar orgName={orgName} user={user} unit={unit} />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          title={activeItem ? t(activeItem.key) : "HRS"}
          user={user}
          notificationCount={notificationCount}
        />
        <main className="flex-1 overflow-y-auto pb-20 md:pb-0">{children}</main>
      </div>
      <BottomTabs />
    </div>
  );
}
