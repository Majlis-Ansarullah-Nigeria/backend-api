"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";
import { cn } from "@/lib/utils";

/** Moves between the Secretariat's national dashboards. */
export function DashboardSwitcher({ current }: { current: "compliance" | "indicators" }) {
  const t = useTranslations("DashboardSwitcher");
  const items = [
    { key: "compliance", href: "/secretariat/dashboards/compliance", label: t("compliance") },
    { key: "indicators", href: "/secretariat/dashboards/indicators", label: t("indicators") },
  ] as const;

  return (
    <nav aria-label={t("label")} className="flex gap-1 border-b border-line">
      {items.map((item) => (
        <Link
          key={item.key}
          href={item.href}
          aria-current={item.key === current ? "page" : undefined}
          className={cn(
            "-mb-px border-b-2 px-3 py-2 text-sm font-semibold",
            item.key === current ? "border-green-600 text-green-700" : "border-transparent text-ink-muted hover:text-ink",
          )}
        >
          {item.label}
        </Link>
      ))}
    </nav>
  );
}
