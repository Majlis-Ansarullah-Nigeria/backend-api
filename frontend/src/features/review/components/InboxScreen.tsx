"use client";

import { Bookmark, Inbox as InboxIcon, Users, X } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/shared/EmptyState";
import { ErrorState } from "@/components/shared/ErrorState";
import { StatusChip } from "@/components/shared/StatusChip";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type {
  InboxFilter,
  InboxItem,
  MeProfile,
  ReportTypeSummary,
  SavedViewSummary,
  SubmissionWindowSummary,
  UnitLevelSummary,
  UnitSummary,
} from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMe } from "@/lib/api/generated/identity/identity";
import { useGetUnitLevels, useGetUnits } from "@/lib/api/generated/organization/organization";
import { useCreateSavedView, useDeleteSavedView, useGetInbox, useGetSavedViews } from "@/lib/api/generated/review/review";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import { useGetSubmissionWindows } from "@/lib/api/generated/windows/windows";
import { cn } from "@/lib/utils";
import { UNIT_LEVEL_CODE, levelLabel } from "@/features/templates/utils/report-type";
import { REPORT_INSTANCE_STATUS, reportInstanceStatusChipVariant } from "@/features/submissions/utils/report-status";
import { InboxDetailPanel } from "./InboxDetailPanel";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

type Tab = "unread" | "flagged" | "mine" | "all";

const INBOX_LEVEL_ORDER = [UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Department];
const INBOX_STATUS_ORDER = [
  REPORT_INSTANCE_STATUS.Submitted,
  REPORT_INSTANCE_STATUS.InReview,
  REPORT_INSTANCE_STATUS.Returned,
  REPORT_INSTANCE_STATUS.Acknowledged,
];

interface FilterState {
  reportTypeId?: string;
  windowId?: string;
  unitLevel?: number;
  zoneUnitId?: string;
  status?: number;
  isLate?: boolean;
  isFlagged?: boolean;
  assignedToUserId?: string;
}

const EMPTY_FILTER: FilterState = {};

/** `initialReportId` (from `?report=`, the search palette) opens that report's detail panel. */
export function InboxScreen({ initialReportId }: { initialReportId?: string } = {}) {
  const fmt = useOrgFormat();
  const t = useTranslations("Inbox");

  const [filter, setFilter] = useState<FilterState>(EMPTY_FILTER);
  const [selectedId, setSelectedId] = useState<string | null>(initialReportId ?? null);
  const [isSavingView, setIsSavingView] = useState(false);
  const [savedViewName, setSavedViewName] = useState("");

  const me = useGetMe<MeProfile, ApiError>();
  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>({ includeArchived: false });
  const nationalReportTypes = (reportTypes.data ?? []).filter((rt) => rt.recipientLevel === UNIT_LEVEL_CODE.National);

  const windows = useGetSubmissionWindows<SubmissionWindowSummary[], ApiError>(filter.reportTypeId ?? "", {
    query: { enabled: !!filter.reportTypeId },
  });

  const levels = useGetUnitLevels<UnitLevelSummary[], ApiError>();
  const zoneLevelId = levels.data?.find((level) => level.code === UNIT_LEVEL_CODE.Zone)?.id;
  const zones = useGetUnits<UnitSummary[], ApiError>({ levelId: zoneLevelId }, { query: { enabled: !!zoneLevelId } });

  const savedViews = useGetSavedViews<SavedViewSummary[], ApiError>();
  const createSavedView = useCreateSavedView();
  const deleteSavedView = useDeleteSavedView();

  const inbox = useGetInbox<InboxItem[], ApiError>(filter);

  const activeTab: Tab | null =
    filter.isFlagged === true && filter.status === undefined && filter.assignedToUserId === undefined
      ? "flagged"
      : filter.isFlagged === undefined && filter.status === REPORT_INSTANCE_STATUS.Submitted && filter.assignedToUserId === undefined
        ? "unread"
        : filter.isFlagged === undefined && filter.status === undefined && filter.assignedToUserId === me.data?.id && me.data
          ? "mine"
          : filter.isFlagged === undefined && filter.status === undefined && filter.assignedToUserId === undefined
            ? "all"
            : null;

  const selectTab = (tab: Tab) => {
    if (tab === "unread") {
      setFilter((current) => ({ ...current, status: REPORT_INSTANCE_STATUS.Submitted, isFlagged: undefined, assignedToUserId: undefined }));
    } else if (tab === "flagged") {
      setFilter((current) => ({ ...current, status: undefined, isFlagged: true, assignedToUserId: undefined }));
    } else if (tab === "mine") {
      setFilter((current) => ({ ...current, status: undefined, isFlagged: undefined, assignedToUserId: me.data?.id }));
    } else {
      setFilter((current) => ({ ...current, status: undefined, isFlagged: undefined, assignedToUserId: undefined }));
    }
  };

  const applySavedView = (savedFilter: InboxFilter) => {
    setFilter({
      reportTypeId: savedFilter.reportTypeId ?? undefined,
      windowId: savedFilter.windowId ?? undefined,
      unitLevel: savedFilter.unitLevel ?? undefined,
      zoneUnitId: savedFilter.zoneUnitId ?? undefined,
      status: savedFilter.status ?? undefined,
      isLate: savedFilter.isLate ?? undefined,
      isFlagged: savedFilter.isFlagged ?? undefined,
      assignedToUserId: savedFilter.assignedToUserId ?? undefined,
    });
  };

  const saveCurrentView = () => {
    if (!savedViewName.trim()) {
      return;
    }
    createSavedView.mutate(
      {
        data: {
          name: savedViewName.trim(),
          filter: {
            reportTypeId: filter.reportTypeId ?? null,
            windowId: filter.windowId ?? null,
            unitLevel: filter.unitLevel ?? null,
            zoneUnitId: filter.zoneUnitId ?? null,
            status: filter.status ?? null,
            isLate: filter.isLate ?? null,
            isFlagged: filter.isFlagged ?? null,
            assignedToUserId: filter.assignedToUserId ?? null,
          },
        },
      },
      {
        onSuccess: () => {
          setIsSavingView(false);
          setSavedViewName("");
          void savedViews.refetch();
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
        </div>
        <Link
          href="/secretariat/workload"
          className="inline-flex h-9 items-center gap-2 rounded-sm border border-line-strong bg-surface px-3 text-sm font-semibold text-ink hover:bg-ground"
        >
          <Users className="size-4" aria-hidden="true" />
          {t("workloadLink")}
        </Link>
      </div>

      <div className="flex gap-1" role="tablist" aria-label={t("tabsLabel")}>
        <FilterTab active={activeTab === "unread"} onClick={() => selectTab("unread")} label={t("tabUnread")} />
        <FilterTab active={activeTab === "flagged"} onClick={() => selectTab("flagged")} label={t("tabFlagged")} />
        <FilterTab active={activeTab === "mine"} onClick={() => selectTab("mine")} label={t("tabMine")} />
        <FilterTab active={activeTab === "all"} onClick={() => selectTab("all")} label={t("tabAll")} />
      </div>

      <div className="flex flex-wrap items-end gap-4 rounded-lg border border-line bg-surface p-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="inbox-report-type">{t("reportTypeLabel")}</Label>
          <select
            id="inbox-report-type"
            value={filter.reportTypeId ?? ""}
            onChange={(event) =>
              setFilter((current) => ({ ...current, reportTypeId: event.target.value || undefined, windowId: undefined }))
            }
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {nationalReportTypes.map((reportType) => (
              <option key={reportType.id} value={reportType.id}>
                {reportType.name}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="inbox-period">{t("periodLabel")}</Label>
          <select
            id="inbox-period"
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
          <Label htmlFor="inbox-level">{t("levelLabel")}</Label>
          <select
            id="inbox-level"
            value={filter.unitLevel ?? ""}
            onChange={(event) =>
              setFilter((current) => ({ ...current, unitLevel: event.target.value === "" ? undefined : Number(event.target.value) }))
            }
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {INBOX_LEVEL_ORDER.map((level) => (
              <option key={level} value={level}>
                {levelLabel(level)}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="inbox-zone">{t("zoneLabel")}</Label>
          <select
            id="inbox-zone"
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

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="inbox-status">{t("statusLabel")}</Label>
          <select
            id="inbox-status"
            value={filter.status ?? ""}
            onChange={(event) =>
              setFilter((current) => ({ ...current, status: event.target.value === "" ? undefined : Number(event.target.value) }))
            }
            className="h-11 rounded-sm border border-line-strong bg-surface px-3 text-sm text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("anyOption")}</option>
            {INBOX_STATUS_ORDER.map((status) => (
              <option key={status} value={status}>
                {statusOptionLabel(t, status)}
              </option>
            ))}
          </select>
        </div>

        <label className="flex items-center gap-2 pb-1">
          <Checkbox
            checked={filter.isLate === true}
            onCheckedChange={(checked) => setFilter((current) => ({ ...current, isLate: checked === true ? true : undefined }))}
          />
          <span className="text-sm text-ink">{t("lateOnlyLabel")}</span>
        </label>

        <label className="flex items-center gap-2 pb-1">
          <Checkbox
            checked={filter.isFlagged === true}
            onCheckedChange={(checked) => setFilter((current) => ({ ...current, isFlagged: checked === true ? true : undefined }))}
          />
          <span className="text-sm text-ink">{t("flaggedOnlyLabel")}</span>
        </label>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {(savedViews.data ?? []).map((view) => (
          <span key={view.id} className="inline-flex items-center gap-1 rounded-full border border-line-strong bg-surface pl-3 pr-1 text-sm">
            <button type="button" className="py-1.5 text-ink hover:text-green-700" onClick={() => applySavedView(view.filter)}>
              {view.name}
            </button>
            <button
              type="button"
              aria-label={t("deleteSavedView", { name: view.name })}
              className="flex size-7 items-center justify-center rounded-full text-ink-muted hover:bg-ground hover:text-danger"
              onClick={() => deleteSavedView.mutate({ id: view.id }, { onSuccess: () => void savedViews.refetch() })}
            >
              <X className="size-3.5" aria-hidden="true" />
            </button>
          </span>
        ))}

        {isSavingView ? (
          <div className="flex items-center gap-2">
            <Input
              autoFocus
              value={savedViewName}
              onChange={(event) => setSavedViewName(event.target.value)}
              placeholder={t("savedViewNamePlaceholder")}
              className="h-9 w-48"
            />
            <Button type="button" size="sm" onClick={saveCurrentView} disabled={createSavedView.isPending}>
              {t("saveSavedView")}
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => setIsSavingView(false)}>
              {t("cancel")}
            </Button>
          </div>
        ) : (
          <Button type="button" variant="outline" size="sm" onClick={() => setIsSavingView(true)}>
            <Bookmark aria-hidden="true" />
            {t("saveCurrentView")}
          </Button>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_420px]">
        <div className="rounded-lg border border-line bg-surface">
          {inbox.isPending ? (
            <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>
          ) : inbox.isError ? (
            <div className="p-2">
              <ErrorState title={t("loadErrorTitle")} onRetry={() => inbox.refetch()} />
            </div>
          ) : inbox.data.length === 0 ? (
            <div className="p-2">
              <EmptyState icon={InboxIcon} title={t("emptyTitle")} description={t("emptyDescription")} />
            </div>
          ) : (
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-muted">
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnReport")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnUnit")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnStatus")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnSubmitted")}
                  </th>
                  <th scope="col" className="px-4 py-3 font-medium">
                    {t("columnAssignedTo")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {inbox.data.map((item) => (
                  <tr
                    key={item.reportId}
                    onClick={() => setSelectedId(item.reportId)}
                    className={cn(
                      "cursor-pointer border-b border-line last:border-0 hover:bg-ground",
                      selectedId === item.reportId && "bg-mint",
                    )}
                  >
                    <td className="px-4 py-3">
                      <p className="font-medium text-ink">{item.reportTypeName}</p>
                      <p className="text-xs text-ink-muted">{item.periodLabel}</p>
                    </td>
                    <td className="px-4 py-3 text-ink-muted">
                      {item.unitName}
                      {item.zoneName ? ` · ${item.zoneName}` : ""}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <StatusChip status={reportInstanceStatusChipVariant(Number(item.status))} />
                        {item.isLate ? <StatusChip status="late" label={t("lateChip")} /> : null}
                        {item.isFlagged ? <StatusChip status="overdue" label={t("flaggedChip")} /> : null}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-ink-muted">
                      {item.submittedAt ? fmt.dateTime(item.submittedAt) : t("notSubmittedYet")}
                    </td>
                    <td className="px-4 py-3 text-ink-muted">{item.assignedToName ?? t("unassigned")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div>
          {selectedId ? (
            <InboxDetailPanel
              reportId={selectedId}
              compareWith={(() => {
                const item = inbox.data?.find((candidate) => candidate.reportId === selectedId);
                return item ? { unitId: item.unitId, reportTypeId: item.reportTypeId, windowId: item.windowId } : undefined;
              })()}
            />
          ) : (
            <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("selectPrompt")}</div>
          )}
        </div>
      </div>
    </div>
  );
}

function statusOptionLabel(t: ReturnType<typeof useTranslations>, status: number): string {
  switch (status) {
    case REPORT_INSTANCE_STATUS.Submitted:
      return t("statusSubmitted");
    case REPORT_INSTANCE_STATUS.InReview:
      return t("statusInReview");
    case REPORT_INSTANCE_STATUS.Returned:
      return t("statusReturned");
    case REPORT_INSTANCE_STATUS.Acknowledged:
      return t("statusAcknowledged");
    default:
      return "";
  }
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
