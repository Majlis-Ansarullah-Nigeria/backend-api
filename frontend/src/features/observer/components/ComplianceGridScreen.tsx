"use client";

import { Bell, Building2 } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/client";
import type { ComplianceGridResult, ComplianceGridRow } from "@/lib/api/generated/hRSApiV1.schemas";
import { sendNudge, useGetComplianceGrid } from "@/lib/api/generated/observer/observer";
import { cn } from "@/lib/utils";
import { REPORT_LINE_STATUS, reportStatusChipVariant } from "@/features/submissions/utils/report-status";

type PeriodsOption = 4 | 12;

type NudgeState = { status: "pending" | "sent" | "error"; message?: string };

export function ComplianceGridScreen() {
  const t = useTranslations("ComplianceGrid");
  const [parentUnitId, setParentUnitId] = useState<string | undefined>(undefined);
  const [reportTypeId, setReportTypeId] = useState<string | undefined>(undefined);
  const [periods, setPeriods] = useState<PeriodsOption>(4);

  const grid = useGetComplianceGrid<ComplianceGridResult, ApiError>({ parentUnitId, reportTypeId, periods });
  const [nudgeState, setNudgeState] = useState<Record<string, NudgeState>>({});

  if (grid.isPending) {
    return <p className="p-4 text-sm text-ink-muted sm:p-6">{t("loading")}</p>;
  }

  if (grid.isError) {
    return (
      <div className="p-4 sm:p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => grid.refetch()} />
      </div>
    );
  }

  const data = grid.data;

  if (data.parentUnitId === null || data.parentUnitId === undefined) {
    return (
      <div className="p-4 sm:p-6">
        <EmptyState icon={Building2} title={t("emptyTitle")} description={t("emptyDescription")} />
      </div>
    );
  }

  const selectParentUnit = (id: string) => {
    setParentUnitId(id);
    setReportTypeId(undefined);
  };

  const latestPeriodIndex = data.periods.length - 1;
  const draftingUnits = latestPeriodIndex >= 0 ? data.rows.filter((r) => r.cells[latestPeriodIndex]?.status === REPORT_LINE_STATUS.Draft) : [];
  const notStartedUnits =
    latestPeriodIndex >= 0 ? data.rows.filter((r) => r.cells[latestPeriodIndex]?.status === REPORT_LINE_STATUS.NotStarted) : [];
  const submittedCount =
    latestPeriodIndex >= 0
      ? data.rows.filter((r) => isSubmittedOrLater(r.cells[latestPeriodIndex]?.status)).length
      : 0;
  const vacantUnits = data.rows.filter((r) => r.hasVacantHead);
  const latestWindowId = latestPeriodIndex >= 0 ? data.periods[latestPeriodIndex]!.windowId : undefined;
  const overdueUnitIds =
    latestPeriodIndex >= 0 ? data.rows.filter((r) => r.cells[latestPeriodIndex]?.status === REPORT_LINE_STATUS.Overdue).map((r) => r.unitId) : [];

  // FR-OBS-003: used by both each row's own Nudge button and "Nudge all overdue" — the latter is
  // just this same single-unit call looped, not a separate bulk endpoint (the FR text only
  // specifies a per-unit action).
  const nudge = async (unitId: string, windowId: string) => {
    setNudgeState((prev) => ({ ...prev, [unitId]: { status: "pending" } }));
    try {
      await sendNudge(unitId, { windowId });
      setNudgeState((prev) => ({ ...prev, [unitId]: { status: "sent" } }));
    } catch (error) {
      const message = error instanceof ApiError ? error.message : t("nudgeFailed");
      setNudgeState((prev) => ({ ...prev, [unitId]: { status: "error", message } }));
    }
  };

  const nudgeAllOverdue = async () => {
    for (const unitId of overdueUnitIds) {
      if (nudgeState[unitId]?.status !== "sent" && latestWindowId) {
        await nudge(unitId, latestWindowId);
      }
    }
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("titleFor", { plural: data.childLevelPluralName! })}</h1>
        <p className="mt-1 text-sm text-ink-muted">
          {t("subtitle", { unitName: data.parentUnitName!, count: data.rows.length, plural: data.childLevelPluralName! })}
        </p>
      </div>

      {data.availableParentUnits.length > 1 ? (
        <div className="flex gap-1" role="tablist" aria-label={t("parentUnitLabel")}>
          {data.availableParentUnits.map((unit) => (
            <FilterTab key={unit.id} active={unit.id === data.parentUnitId} onClick={() => selectParentUnit(unit.id)} label={unit.name} />
          ))}
        </div>
      ) : null}

      {data.availableReportTypes.length === 0 ? (
        <EmptyState icon={Building2} title={t("noReportTypesTitle")} description={t("noReportTypesDescription")} />
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex gap-1" role="tablist" aria-label={t("columnUnit")}>
              {data.availableReportTypes.map((reportType) => (
                <FilterTab
                  key={reportType.id}
                  active={reportType.id === data.selectedReportTypeId}
                  onClick={() => setReportTypeId(reportType.id)}
                  label={reportType.name}
                />
              ))}
            </div>
            <div className="flex gap-1" role="tablist" aria-label={t("last4Periods")}>
              <FilterTab active={periods === 4} onClick={() => setPeriods(4)} label={t("last4Periods")} />
              <FilterTab active={periods === 12} onClick={() => setPeriods(12)} label={t("last12Periods")} />
            </div>
          </div>

          {data.periods.length === 0 ? (
            <EmptyState icon={Building2} title={t("noPeriodsTitle")} description={t("noPeriodsDescription")} />
          ) : (
            <>
              {vacantUnits.length > 0 ? (
                <div className="rounded-lg border border-status-overdue-bg bg-status-overdue-bg/40 px-4 py-3 text-sm font-medium text-status-overdue-fg">
                  {t("vacantBannerLabel", { names: vacantUnits.map((u) => u.unitName).join(", ") })}
                </div>
              ) : null}

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <SummaryCard label={t("submittedCardLabel", { period: data.periods[latestPeriodIndex]!.periodLabel })} value={`${submittedCount} / ${data.rows.length}`} />
                <SummaryCard label={t("draftingCardLabel")} value={draftingUnits.length} detail={draftingUnits.map((u) => u.unitName).join(", ")} />
                <SummaryCard label={t("notStartedCardLabel")} value={notStartedUnits.length} detail={notStartedUnits.map((u) => u.unitName).join(", ")} />
              </div>

              {overdueUnitIds.length > 0 ? (
                <div className="flex justify-end">
                  <Button type="button" variant="outline" size="sm" onClick={() => void nudgeAllOverdue()}>
                    <Bell className="size-4" aria-hidden="true" />
                    {t("nudgeAllOverdue", { count: overdueUnitIds.length })}
                  </Button>
                </div>
              ) : null}

              <div className="overflow-x-auto rounded-lg border border-line bg-surface">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-muted">
                      <th scope="col" className="whitespace-nowrap px-4 py-3 font-medium">
                        {t("columnUnit")}
                      </th>
                      {data.periods.map((period) => (
                        <th key={period.windowId} scope="col" className="whitespace-nowrap px-4 py-3 font-medium">
                          {period.periodLabel}
                        </th>
                      ))}
                      <th scope="col" className="whitespace-nowrap px-4 py-3 font-medium">
                        <span className="sr-only">{t("nudgeColumnLabel")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.rows.map((row) => (
                      <GridRow
                        key={row.unitId}
                        row={row}
                        headFallback={t("headColumnFallback")}
                        latestWindowId={latestWindowId}
                        nudgeState={nudgeState[row.unitId]}
                        onNudge={nudge}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}

function GridRow({
  row,
  headFallback,
  latestWindowId,
  nudgeState,
  onNudge,
}: {
  row: ComplianceGridRow;
  headFallback: string;
  latestWindowId: string | undefined;
  nudgeState: NudgeState | undefined;
  onNudge: (unitId: string, windowId: string) => void;
}) {
  const t = useTranslations("ComplianceGrid");
  const latestCell = row.cells.at(-1);
  const isOverdue = latestCell?.status === REPORT_LINE_STATUS.Overdue && latestCell.windowId === latestWindowId;

  return (
    <tr className="border-b border-line last:border-0">
      <td className="px-4 py-3">
        <p className="font-medium text-ink">{row.unitName}</p>
        <p className={cn("text-xs", row.hasVacantHead ? "font-semibold text-danger" : "text-ink-muted")}>{row.headName ?? headFallback}</p>
      </td>
      {row.cells.map((cell) =>
        cell.reportInstanceId ? (
          <td key={cell.windowId} className="px-4 py-3">
            <Link href={`/units/${row.unitId}/reports/${cell.reportInstanceId}`} className="inline-block">
              <StatusChip status={reportStatusChipVariant(cell.status)} />
            </Link>
          </td>
        ) : (
          <td key={cell.windowId} className="px-4 py-3">
            <StatusChip status={reportStatusChipVariant(cell.status)} />
          </td>
        ),
      )}
      <td className="px-4 py-3 text-right">
        {isOverdue && latestWindowId ? (
          nudgeState?.status === "sent" ? (
            <span className="text-xs font-semibold text-green-700">{t("nudgeSent")}</span>
          ) : (
            <div className="flex flex-col items-end gap-1">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => onNudge(row.unitId, latestWindowId)}
                disabled={nudgeState?.status === "pending"}
              >
                <Bell className="size-3.5" aria-hidden="true" />
                {t("nudgeButton")}
              </Button>
              {nudgeState?.status === "error" ? <span className="text-xs text-danger">{nudgeState.message}</span> : null}
            </div>
          )
        ) : null}
      </td>
    </tr>
  );
}

function SummaryCard({ label, value, detail }: { label: string; value: string | number; detail?: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{label}</p>
      <p className="mt-1 text-2xl font-bold text-ink">{value}</p>
      {detail ? <p className="mt-1 truncate text-xs text-ink-muted">{detail}</p> : null}
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

function isSubmittedOrLater(status: number | undefined): boolean {
  return (
    status === REPORT_LINE_STATUS.Submitted ||
    status === REPORT_LINE_STATUS.SubmittedLate ||
    status === REPORT_LINE_STATUS.InReview ||
    status === REPORT_LINE_STATUS.Acknowledged
  );
}
