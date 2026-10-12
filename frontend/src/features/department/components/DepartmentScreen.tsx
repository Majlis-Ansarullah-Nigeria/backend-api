"use client";

import { Building2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import type { DepartmentReportRow, ReportTypeSummary, SubmissionWindowSummary, UnitLevelSummary, UnitSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetDepartmentReports } from "@/lib/api/generated/department/department";
import { useGetUnitLevels, useGetUnits } from "@/lib/api/generated/organization/organization";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import { useGetSubmissionWindows } from "@/lib/api/generated/windows/windows";
import { UNIT_LEVEL_CODE, levelLabel } from "@/features/templates/utils/report-type";
import { reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";

const DEPARTMENT_LEVEL_ORDER = [UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Department];

interface FilterState {
  reportTypeId?: string;
  windowId?: string;
  unitLevel?: number;
  zoneUnitId?: string;
}

export function DepartmentScreen() {
  const t = useTranslations("Department");

  const [filter, setFilter] = useState<FilterState>({});

  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>({ includeArchived: false });
  const windows = useGetSubmissionWindows<SubmissionWindowSummary[], ApiError>(filter.reportTypeId ?? "", {
    query: { enabled: !!filter.reportTypeId },
  });
  const levels = useGetUnitLevels<UnitLevelSummary[], ApiError>();
  const zoneLevelId = levels.data?.find((level) => level.code === UNIT_LEVEL_CODE.Zone)?.id;
  const zones = useGetUnits<UnitSummary[], ApiError>({ levelId: zoneLevelId }, { query: { enabled: !!zoneLevelId } });

  const reports = useGetDepartmentReports<DepartmentReportRow[], ApiError>(filter);

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="flex flex-wrap items-end gap-4 rounded-lg border border-line bg-surface p-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dept-report-type">{t("reportTypeLabel")}</Label>
          <select
            id="dept-report-type"
            value={filter.reportTypeId ?? ""}
            onChange={(event) =>
              setFilter((current) => ({ ...current, reportTypeId: event.target.value || undefined, windowId: undefined }))
            }
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {(reportTypes.data ?? []).map((reportType) => (
              <option key={reportType.id} value={reportType.id}>
                {reportType.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dept-period">{t("periodLabel")}</Label>
          <select
            id="dept-period"
            value={filter.windowId ?? ""}
            disabled={!filter.reportTypeId}
            onChange={(event) => setFilter((current) => ({ ...current, windowId: event.target.value || undefined }))}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40 disabled:opacity-60"
          >
            <option value="">{t("anyOption")}</option>
            {(windows.data ?? []).map((window) => (
              <option key={window.id} value={window.id}>
                {window.periodLabel}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dept-level">{t("levelLabel")}</Label>
          <select
            id="dept-level"
            value={filter.unitLevel ?? ""}
            onChange={(event) =>
              setFilter((current) => ({ ...current, unitLevel: event.target.value === "" ? undefined : Number(event.target.value) }))
            }
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {DEPARTMENT_LEVEL_ORDER.map((level) => (
              <option key={level} value={level}>
                {levelLabel(level)}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dept-zone">{t("zoneLabel")}</Label>
          <select
            id="dept-zone"
            value={filter.zoneUnitId ?? ""}
            onChange={(event) => setFilter((current) => ({ ...current, zoneUnitId: event.target.value || undefined }))}
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {(zones.data ?? []).map((zone) => (
              <option key={zone.id} value={zone.id}>
                {zone.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {reports.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : reports.isError ? (
        reports.error instanceof ApiError && reports.error.code === "FORBIDDEN" ? (
          <EmptyState icon={Building2} title={t("notAnOfficerTitle")} description={t("notAnOfficerDescription")} />
        ) : (
          <ErrorState title={t("loadErrorTitle")} onRetry={() => reports.refetch()} />
        )
      ) : reports.data.length === 0 ? (
        <EmptyState icon={Building2} title={t("emptyTitle")} description={t("emptyDescription")} />
      ) : (
        <div className="flex flex-col gap-4">
          {reports.data.map((row) => (
            <div key={row.reportId} className="rounded-lg border border-line bg-surface p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <h2 className="font-bold text-ink">
                    {row.unitName}
                    {row.zoneName ? ` · ${row.zoneName}` : ""}
                  </h2>
                  <p className="text-sm text-ink-muted">
                    {row.reportTypeName} · {row.periodLabel}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <StatusChip status={reportInstanceStatusChipVariant(Number(row.status))} />
                  {row.submittedAt ? (
                    <span className="text-xs text-ink-muted">{t("submittedMeta", { date: new Date(row.submittedAt).toLocaleString() })}</span>
                  ) : null}
                </div>
              </div>

              <dl className="mt-3 flex flex-col gap-2">
                {row.fields.map((field) => (
                  <div key={field.fieldKey} className="flex items-baseline justify-between gap-3 text-sm">
                    <dt className="text-ink-muted">{field.label}</dt>
                    <dd className="text-right font-medium text-ink">{field.value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
