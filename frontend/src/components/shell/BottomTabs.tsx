"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { isNavItemActive, PRIMARY_NAV_ITEMS } from "./nav-items";

export function BottomTabs() {
  const pathname = usePathname();
  const t = useTranslations("Shell.nav");

  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-40 flex items-stretch justify-around border-t border-white/10 bg-forest-900 pb-[env(safe-area-inset-bottom,0px)] md:hidden"
    >
      {PRIMARY_NAV_ITEMS.map((item) => {
        const active = isNavItemActive(item, pathname);
        const Icon = item.icon;

        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className="flex min-h-[56px] min-w-11 flex-1 flex-col items-center justify-center gap-1 py-2 text-[11px] font-medium text-white/60"
          >
            <span
              className={cn(
                "flex items-center justify-center rounded-full px-3 py-1 transition-colors",
                active && "bg-lime",
              )}
            >
              <Icon className={cn("size-5", active ? "text-forest-900" : "text-white/60")} aria-hidden="true" />
            </span>
            <span className={cn(active && "font-semibold text-lime")}>{t(`${item.key}Short`)}</span>
          </Link>
        );
      })}
    </nav>
  );
}
