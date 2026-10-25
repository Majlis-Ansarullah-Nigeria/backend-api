"use client";

import { ChevronRight } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { ReportVersionsPanel } from "@/features/history/components/ReportVersionsPanel";

export function ReportVersionsScreen({ reportId }: { reportId: string }) {
  const t = useTranslations("Versions");

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <nav aria-label={t("breadcrumbLabel")} className="flex items-center gap-2 text-sm">
        <Link href="/history" className="text-ink-muted hover:text-ink">
          {t("breadcrumbHistory")}
        </Link>
        <ChevronRight className="size-4 text-ink-muted" aria-hidden="true" />
        <span className="font-semibold text-ink" aria-current="page">
          {t("title")}
        </span>
      </nav>
      <ReportVersionsPanel reportId={reportId} headingLevel={1} />
    </div>
  );
}
