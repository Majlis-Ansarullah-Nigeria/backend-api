"use client";

import { FileText, Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { ReportTypeSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import { cn } from "@/lib/utils";
import { applicableLevelsLabel, categoryLabel, frequencyLabel } from "@/features/templates/utils/report-type";
import { CreateReportTypePanel } from "./CreateReportTypePanel";
import { ReportTypeDetailPanel } from "./ReportTypeDetailPanel";

type Tab = "active" | "archived";

/** `startCreating` (from `?new=1`, the search palette's "Create report type") opens the create panel. */
export function ReportTypesScreen({ startCreating = false }: { startCreating?: boolean } = {}) {
  const t = useTranslations("ReportTypes");
  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>({ includeArchived: true });

  const [tab, setTab] = useState<Tab>("active");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(startCreating);

  if (reportTypes.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (reportTypes.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("listErrorTitle")} description={t("listErrorDescription")} onRetry={() => reportTypes.refetch()} />
      </div>
    );
  }

  const visible = reportTypes.data.filter((reportType) =>
    tab === "archived" ? reportType.archivedAt !== null : reportType.archivedAt === null,
  );

  const selectReportType = (id: string) => {
    setSelectedId(id);
    setIsCreating(false);
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
        </div>
        <Button
          onClick={() => {
            setIsCreating(true);
            setSelectedId(null);
          }}
        >
          <Plus aria-hidden="true" />
          {t("newReportType")}
        </Button>
      </div>

      <div className="flex gap-1" role="tablist" aria-label={t("filterLabel")}>
        <FilterTab active={tab === "active"} onClick={() => setTab("active")} label={t("tabActive")} />
        <FilterTab active={tab === "archived"} onClick={() => setTab("archived")} label={t("tabArchived")} />
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_420px]">
        <div className="rounded-lg border border-line bg-surface">
          {visible.length === 0 ? (
            <div className="p-2">
              <EmptyState icon={FileText} title={t("emptyTitle")} description={t("emptyDescription")} />
            </div>
          ) : (
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-muted">
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnName")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnWhoSubmits")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnAttachments")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnNilReport")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {visible.map((reportType) => (
                  <tr
                    key={reportType.id}
                    onClick={() => selectReportType(reportType.id)}
                    className={cn(
                      "cursor-pointer border-b border-line last:border-0 hover:bg-ground",
                      selectedId === reportType.id && "bg-mint",
                    )}
                  >
                    <td className="px-4 py-3">
                      <p className="font-medium text-ink">{reportType.name}</p>
                      <p className="text-xs text-ink-muted">
                        {categoryLabel(reportType.category)} · {frequencyLabel(reportType.frequency)}
                      </p>
                    </td>
                    <td className="px-4 py-3 text-ink-muted">{applicableLevelsLabel(reportType.applicableLevels)}</td>
                    <td className="px-4 py-3 text-ink-muted">{reportType.allowAttachments ? t("yes") : t("no")}</td>
                    <td className="px-4 py-3 text-ink-muted">{reportType.allowNilReport ? t("yes") : t("no")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div>
          {isCreating ? (
            <CreateReportTypePanel
              onCancel={() => setIsCreating(false)}
              onCreated={(id) => {
                setIsCreating(false);
                selectReportType(id);
              }}
            />
          ) : selectedId ? (
            <ReportTypeDetailPanel reportTypeId={selectedId} />
          ) : (
            <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("selectPrompt")}</div>
          )}
        </div>
      </div>
    </div>
  );
}

function FilterTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "min-h-9 rounded-md px-3 text-sm font-medium transition-colors",
        active ? "bg-mint text-green-700" : "text-ink-muted hover:bg-ground",
      )}
    >
      {label}
    </button>
  );
}
