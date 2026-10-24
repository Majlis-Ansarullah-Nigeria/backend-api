"use client";

import { useTranslations } from "next-intl";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useNotificationBadge } from "@/features/notifications/hooks/useNotificationBadge";
import type { CSSProperties } from "react";
import type { DashboardAccess, OrganizationSettingsDto } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetDashboardAccess } from "@/lib/api/generated/dashboards/dashboards";
import { useGetOrganizationSettings } from "@/lib/api/generated/settings/settings";
import { OrgFormatProvider } from "@/lib/org/OrgFormatContext";
import { BottomTabs } from "./BottomTabs";
import { COMPILED_NAV_ITEM, DASHBOARD_NAV_ITEM, isNavItemActive, PRIMARY_NAV_ITEMS, SECRETARIAT_NAV_ITEMS, type NavItem } from "./nav-items";
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

  // /admin (settings) sits with the Secretariat's screens.
  const isSecretariat = (pathname?.startsWith("/secretariat") || pathname?.startsWith("/admin")) ?? false;
  // FR-ADM-001: name, logo, brand colour, time zone and date format for every screen.
  const settings = useGetOrganizationSettings<OrganizationSettingsDto>({ query: { staleTime: 5 * 60 * 1000 } });
  const brand = settings.data;
  const brandStyle = brand
    ? ({ "--color-green-600": brand.brandPrimary, "--color-green-700": brand.brandDark } as CSSProperties)
    : undefined;
  // FR-DSH-005: Zone/Dila heads and Leadership get a Dashboard link. Navigation only — the
  // dashboard API checks scope on every request.
  const dashboardAccess = useGetDashboardAccess<DashboardAccess>({ query: { enabled: !isSecretariat, staleTime: 5 * 60 * 1000 } });
  const extraItems: NavItem[] = [
    ...(dashboardAccess.data?.hasIndicators ? [DASHBOARD_NAV_ITEM] : []),
    ...(dashboardAccess.data?.hasCompiledReports ? [COMPILED_NAV_ITEM] : []),
  ];
  const navItems = isSecretariat
    ? SECRETARIAT_NAV_ITEMS
    : [...PRIMARY_NAV_ITEMS.slice(0, 4), ...extraItems, ...PRIMARY_NAV_ITEMS.slice(4)];

  const activeItem = navItems.find((item) => isNavItemActive(item, pathname));

  return (
    <div className="flex min-h-dvh" style={brandStyle}>
      <Sidebar
        orgName={brand?.name ?? orgName}
        logoUrl={brand?.hasLogo ? `/api/v1/settings/logo?v=${brand.logoVersion ?? ""}` : null}
        user={user}
        unit={unit}
        navItems={navItems}
      />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          title={activeItem ? t(activeItem.key) : "HRS"}
          user={user}
          notificationCount={notificationCount}
        />
        <main className="flex-1 overflow-y-auto pb-20 md:pb-0">
          <OrgFormatProvider settings={brand ? { timeZone: brand.timeZone, dateFormat: Number(brand.dateFormat) } : null}>{children}</OrgFormatProvider>
        </main>
      </div>
      {isSecretariat ? null : <BottomTabs />}
    </div>
  );
}
