"use client";

import { useTranslations } from "next-intl";
import { usePathname } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { FEATURE_MODULE, useFeatureFlags } from "@/features/admin/hooks/useFeatureFlags";
import { useNotificationBadge } from "@/features/notifications/hooks/useNotificationBadge";
import type { CSSProperties } from "react";
import type { DashboardAccess, MeProfile, OrganizationSettingsDto, RoleAssignmentSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetDashboardAccess } from "@/lib/api/generated/dashboards/dashboards";
import { useGetMe, useLogout, useSetActiveAssignment } from "@/lib/api/generated/identity/identity";
import { useGetOrganizationSettings } from "@/lib/api/generated/settings/settings";
import { OrgFormatProvider } from "@/lib/org/OrgFormatContext";
import { BottomTabs } from "./BottomTabs";
import { CommandPalette } from "./CommandPalette";
import { activeAssignment, initials, roleLabelKey } from "./current-user";
import { COMPILED_NAV_ITEM, DASHBOARD_NAV_ITEM, isNavItemActive, PRIMARY_NAV_ITEMS, SECRETARIAT_NAV_ITEMS, type NavItem } from "./nav-items";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import type { UnitOption } from "./UnitSwitcher";

export interface AppShellProps {
  children: ReactNode;
  orgName?: string;
}

export function AppShell({ children, orgName }: AppShellProps) {
  const pathname = usePathname();
  const t = useTranslations("Shell.nav");
  const tShell = useTranslations("Shell");
  const router = useRouter();
  const queryClient = useQueryClient();

  // The signed-in user and the unit they're acting as (FR-AC-002), from GET /me.
  const me = useGetMe<MeProfile>({ query: { staleTime: 5 * 60 * 1000 } });
  const setActive = useSetActiveAssignment();
  const logout = useLogout();
  const toOption = (assignment: RoleAssignmentSummary): UnitOption => ({
    id: assignment.id,
    name: assignment.unitName,
    role: tShell(`roles.${roleLabelKey(assignment)}`, { level: assignment.unitLevelName }),
  });
  const current = activeAssignment(me.data);
  const user = { name: me.data?.name ?? "", initials: initials(me.data?.name ?? "") };
  const unit: UnitOption = current ? toOption(current) : { id: "", name: me.data ? tShell("noUnit") : "", role: "" };
  const unitOptions = (me.data?.roleAssignments ?? []).map(toOption);
  const selectUnit = (option: UnitOption) => {
    if (option.id === current?.id) {
      return;
    }
    // Everything on screen may depend on the unit acted as, so refetch it all.
    setActive.mutate({ data: { roleAssignmentId: option.id } }, { onSuccess: () => queryClient.invalidateQueries() });
  };
  const signOut = () =>
    logout.mutate(undefined, {
      onSettled: () => {
        queryClient.clear();
        router.replace("/login");
      },
    });
  const notificationCount = useNotificationBadge();
  const [searchOpen, setSearchOpen] = useState(false);

  // FR-SRC-001: Ctrl/Cmd+K opens the search palette from anywhere in the app.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen(true);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

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
  // FR-ADM-004: a switched-off Dashboards module drops its link (the head-side links above already
  // follow it, since dashboard access is ANDed with the flags server-side).
  const { isEnabled } = useFeatureFlags();
  const navItems = isSecretariat
    ? SECRETARIAT_NAV_ITEMS.filter((item) => item.key !== "dashboards" || isEnabled(FEATURE_MODULE.Dashboards))
    : [...PRIMARY_NAV_ITEMS.slice(0, 4), ...extraItems, ...PRIMARY_NAV_ITEMS.slice(4)];

  const activeItem = navItems.find((item) => isNavItemActive(item, pathname));

  return (
    <div className="flex min-h-dvh" style={brandStyle}>
      {/* WCAG 2.4.1: lets keyboard users jump past the sidebar's navigation on every page. */}
      <a
        href="#main-content"
        className="sr-only z-50 rounded-md bg-surface px-4 py-2 text-sm font-semibold text-green-700 shadow focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:outline-none focus:ring-2 focus:ring-green-600"
      >
        {tShell("skipToContent")}
      </a>
      <Sidebar
        orgName={brand?.name ?? orgName}
        logoUrl={brand?.hasLogo ? `/api/v1/settings/logo?v=${brand.logoVersion ?? ""}` : null}
        user={user}
        unit={unit}
        unitOptions={unitOptions}
        onSelectUnit={selectUnit}
        onSignOut={signOut}
        navItems={navItems}
      />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          title={activeItem ? t(activeItem.key) : "HRS"}
          user={user}
          notificationCount={notificationCount}
          onOpenSearch={() => setSearchOpen(true)}
        />
        <main id="main-content" tabIndex={-1} className="flex-1 overflow-y-auto pb-20 outline-none md:pb-0">
          <OrgFormatProvider settings={brand ? { timeZone: brand.timeZone, dateFormat: Number(brand.dateFormat) } : null}>{children}</OrgFormatProvider>
        </main>
      </div>
      {isSecretariat ? null : <BottomTabs />}
      <CommandPalette open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
