"use client";

import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ErrorState } from "@/components/shared/ErrorState";
import type { ApiError } from "@/lib/api/client";
import type { ReportTypeSummary, SubmissionWindowSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetOrganizationTree } from "@/lib/api/generated/organization/organization";
import { useGetReportTypes } from "@/lib/api/generated/templates/templates";
import {
  getGetSubmissionWindowQueryKey,
  getGetSubmissionWindowsQueryKey,
  useCreateSubmissionWindow,
  useGetSubmissionWindow,
  useUpdateSubmissionWindow,
} from "@/lib/api/generated/windows/windows";
import type { WindowFormValues } from "@/features/windows/schemas/window-form";
import { ExtensionsPanel } from "./ExtensionsPanel";
import { ReopenPanel } from "./ReopenPanel";
import { WindowForm } from "./WindowForm";

const DEFAULT_REMINDER_SETTINGS = {
  remindFiveDaysBefore: true,
  remindTwoDaysBefore: true,
  remindOnDueDay: true,
  escalateToParentAfterThreeDays: true,
  escalateToZoneAfterSevenDays: false,
};

export function WindowFormScreen({ windowId, reportTypeIdParam }: { windowId?: string; reportTypeIdParam?: string }) {
  const t = useTranslations("Windows");
  const router = useRouter();
  const queryClient = useQueryClient();
  const [lastWarnings, setLastWarnings] = useState<string[]>([]);

  const reportTypes = useGetReportTypes<ReportTypeSummary[], ApiError>({ includeArchived: false });
  const units = useGetOrganizationTree<UnitTreeNode[], ApiError>({ includeArchived: false });
  const existingWindow = useGetSubmissionWindow<SubmissionWindowSummary, ApiError>(reportTypeIdParam ?? "", windowId ?? "", {
    query: { enabled: !!windowId && !!reportTypeIdParam },
  });

  const createWindow = useCreateSubmissionWindow<ApiError>();
  const updateWindow = useUpdateSubmissionWindow<ApiError>();

  const stillLoading = reportTypes.isPending || units.isPending || (!!windowId && existingWindow.isPending);
  const hasError = reportTypes.isError || units.isError || (!!windowId && existingWindow.isError);

  if (stillLoading) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (hasError) {
    return (
      <div className="p-6">
        <ErrorState
          title={t("loadErrorTitle")}
          onRetry={() => {
            reportTypes.refetch();
            units.refetch();
            if (windowId) {
              existingWindow.refetch();
            }
          }}
        />
      </div>
    );
  }

  const existing = windowId ? existingWindow.data : undefined;

  const unitCountsByLevel: Record<number, number> = {};
  const unitOptions = units.data
    .filter((unit) => !unit.archivedAt)
    .map((unit) => {
      unitCountsByLevel[unit.levelCode] = (unitCountsByLevel[unit.levelCode] ?? 0) + 1;
      return { id: unit.id, name: unit.name };
    });

  const defaultValues: WindowFormValues = existing
    ? {
        reportTypeId: existing.reportTypeId,
        periodLabel: existing.periodLabel,
        opensDate: existing.opensAt.slice(0, 10),
        graceDays: String(existing.graceDays),
        closesDate: existing.closesAt.slice(0, 10),
        dueDates: existing.dueDates.map((dueDate) => ({
          level: String(dueDate.level),
          dueDate: dueDate.dueAt.slice(0, 10),
          dueTime: dueDate.dueAt.slice(11, 16),
        })),
        targetUnitIds: existing.targetUnitIds,
        reminderSettings: existing.reminderSettings,
      }
    : {
        reportTypeId: reportTypeIdParam ?? "",
        periodLabel: "",
        opensDate: "",
        graceDays: "3",
        closesDate: "",
        dueDates: [],
        targetUnitIds: [],
        reminderSettings: DEFAULT_REMINDER_SETTINGS,
      };

  const onSubmit = (values: WindowFormValues) => {
    const payload = {
      periodLabel: values.periodLabel,
      opensAt: new Date(`${values.opensDate}T00:00:00`).toISOString(),
      graceDays: Number(values.graceDays),
      closesAt: new Date(`${values.closesDate}T23:59:59`).toISOString(),
      dueDates: values.dueDates.map((row) => ({
        level: Number(row.level),
        dueAt: new Date(`${row.dueDate}T${row.dueTime}:00`).toISOString(),
      })),
      targetUnitIds: values.targetUnitIds.length > 0 ? values.targetUnitIds : null,
      reminderSettings: values.reminderSettings,
    };

    if (existing) {
      updateWindow.mutate(
        { reportTypeId: existing.reportTypeId, windowId: existing.id, data: payload },
        {
          onSuccess: (result) => {
            setLastWarnings(result.warnings);
            queryClient.invalidateQueries({ queryKey: getGetSubmissionWindowQueryKey(existing.reportTypeId, existing.id) });
            queryClient.invalidateQueries({ queryKey: getGetSubmissionWindowsQueryKey(existing.reportTypeId) });
          },
        },
      );
    } else {
      createWindow.mutate(
        { reportTypeId: values.reportTypeId, data: payload },
        {
          onSuccess: (result) => {
            queryClient.invalidateQueries({ queryKey: getGetSubmissionWindowsQueryKey(values.reportTypeId) });
            router.push(`/secretariat/windows/${result.window.id}?reportTypeId=${values.reportTypeId}`);
          },
        },
      );
    }
  };

  return (
    <div className="flex flex-col gap-6 p-4 sm:p-6">
      <div className="flex items-start gap-3">
        <Link
          href="/secretariat/windows"
          aria-label={t("backToWindows")}
          className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-md border border-line-strong hover:bg-ground"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-ink">{existing ? existing.periodLabel : t("newWindowTitle")}</h1>
          {existing ? <p className="mt-1 text-sm text-ink-muted">{t("editingNote")}</p> : null}
        </div>
      </div>

      <WindowForm
        reportTypes={reportTypes.data}
        unitCountsByLevel={unitCountsByLevel}
        defaultValues={defaultValues}
        reportTypeLocked={!!existing}
        onSubmit={onSubmit}
        onCancel={() => router.push("/secretariat/windows")}
        submitLabel={existing ? t("saveWindow") : t("createWindow")}
        pendingLabel={t("saving")}
        isPending={createWindow.isPending || updateWindow.isPending}
        errorMessage={
          createWindow.isError ? createWindow.error.message : updateWindow.isError ? updateWindow.error.message : undefined
        }
        serverWarnings={lastWarnings}
      />

      {existing ? (
        <>
          <ExtensionsPanel
            reportTypeId={existing.reportTypeId}
            windowId={existing.id}
            extensions={existing.extensions}
            units={unitOptions}
          />
          <ReopenPanel reportTypeId={existing.reportTypeId} windowId={existing.id} reopens={existing.reopens} units={unitOptions} />
        </>
      ) : null}
    </div>
  );
}
