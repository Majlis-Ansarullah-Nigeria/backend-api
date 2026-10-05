"use client";

import { BarChart3, LogOut } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { cn } from "@/lib/utils";
import { PRIMARY_NAV_ITEMS } from "./nav-items";
import type { UnitOption } from "./UnitSwitcher";
import { UnitSwitcher } from "./UnitSwitcher";

export interface SidebarProps {
  orgName?: string;
  user: { name: string; initials: string };
  unit: UnitOption;
}

export function Sidebar({ orgName, user, unit }: SidebarProps) {
  const pathname = usePathname();
  const t = useTranslations("Shell");

  return (
    <aside className="hidden h-dvh w-72 shrink-0 flex-col bg-forest-900 text-white md:flex">
      <div className="flex items-center gap-3 px-5 pb-4 pt-6">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-green-600">
          <BarChart3 className="size-5 text-lime" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-bold leading-tight">{t("appName")}</p>
          <p className="truncate text-xs text-white/60">{orgName ?? t("orgPlaceholder")}</p>
        </div>
      </div>

      <div className="px-4 pb-4">
        <UnitSwitcher current={unit} />
      </div>

      <nav className="flex-1 space-y-1 overflow-y-auto px-3" aria-label="Primary">
        {PRIMARY_NAV_ITEMS.map((item) => {
          const active = pathname === item.href || pathname?.startsWith(`${item.href}/`);
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "flex min-h-11 items-center gap-3 rounded-md border-l-4 border-transparent px-3 py-2.5 text-sm font-medium text-white/70 transition-colors hover:bg-white/5 hover:text-white",
                active && "border-lime bg-white/10 font-semibold text-white",
              )}
            >
              <Icon className="size-5 shrink-0" aria-hidden="true" />
              {t(`nav.${item.key}`)}
            </Link>
          );
        })}
      </nav>

      <div className="flex items-center gap-3 border-t border-white/10 px-4 py-4">
        <Avatar>
          <AvatarFallback>{user.initials}</AvatarFallback>
        </Avatar>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{user.name}</p>
          <p className="text-xs text-white/60">{t("signedIn")}</p>
        </div>
        <button
          type="button"
          aria-label={t("signOut")}
          className="rounded-md p-2 text-white/60 transition-colors hover:bg-white/10 hover:text-white"
        >
          <LogOut className="size-4" aria-hidden="true" />
        </button>
      </div>
    </aside>
  );
}
