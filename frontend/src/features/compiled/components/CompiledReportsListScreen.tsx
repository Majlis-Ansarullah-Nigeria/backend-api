"use client";

import { FileText } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { ApiError } from "@/lib/api/client";
import type { CompiledReportListItem } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetCompiledReports } from "@/lib/api/generated/compiled-reports/compiled-reports";

/** Every period that can be compiled into a national report (FR-REV-006). */
export function CompiledReportsListScreen({ basePath }: { basePath: string }) {
  const t = useTranslations("Compiled");
  const query = useGetCompiledReports<CompiledReportListItem[], ApiError>();

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink sm:text-3xl">{t("listTitle")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("listSubtitle")}</p>
      </div>
      {query.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : query.isError ? (
        <ErrorState title={t("loadErrorTitle")} onRetry={() => query.refetch()} />
      ) : query.data.length === 0 ? (
        <EmptyState icon={FileText} title={t("listEmptyTitle")} description={t("listEmptyDescription")} />
      ) : (
        <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
          {query.data.map((item) => (
            <li key={item.windowId}>
              <Link href={`${basePath}/${item.windowId}`} className="flex items-center justify-between gap-3 px-4 py-3.5 hover:bg-ground">
                <span>
                  <span className="block font-semibold text-ink">{item.periodLabel}</span>
                  <span className="block text-sm text-ink-muted">{item.reportTypeName}</span>
                </span>
                <span className="text-sm text-ink-muted">
                  {[item.isClosed ? t("windowClosed") : t("windowOpen"), item.hasNarrative ? t("hasSummary") : null].filter(Boolean).join(" · ")}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
