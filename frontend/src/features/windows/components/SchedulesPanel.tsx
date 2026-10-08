"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { frequencyLabel, levelLabel, REPORT_TYPE_FREQUENCY, UNIT_LEVEL_CODE } from "@/features/templates/utils/report-type";
import type { ApiError } from "@/lib/api/client";
import type { ReportTypeSummary, WindowScheduleSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetSubmissionWindowsQueryKey,
  getGetWindowSchedulesQueryKey,
  useActivateWindowSchedule,
  useCreateWindowSchedule,
  useDeactivateWindowSchedule,
  useGenerateScheduledWindows,
  useGetWindowSchedules,
} from "@/lib/api/generated/windows/windows";

const SCHEDULE_FREQUENCIES = [REPORT_TYPE_FREQUENCY.Monthly, REPORT_TYPE_FREQUENCY.Quarterly, REPORT_TYPE_FREQUENCY.Annual];
const DUE_OFFSET_LEVEL_ORDER = [UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Department];

export function SchedulesPanel({ reportTypes, onClose }: { reportTypes: ReportTypeSummary[]; onClose: () => void }) {
  const t = useTranslations("Windows");
  const [reportTypeId, setReportTypeId] = useState(reportTypes[0]?.id ?? "");
  const reportType = reportTypes.find((entry) => entry.id === reportTypeId);

  const schedules = useGetWindowSchedules<WindowScheduleSummary[], ApiError>(reportTypeId, {
    query: { enabled: !!reportTypeId },
  });

  return (
    <div className="rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-bold text-ink">{t("schedulesTitle")}</h2>
        <Button type="button" size="sm" variant="outline" onClick={onClose}>
          {t("close")}
        </Button>
      </div>

      <div className="mt-4 flex flex-col gap-1.5">
        <Label htmlFor="schedules-report-type">{t("reportTypeLabel")}</Label>
        <select
          id="schedules-report-type"
          value={reportTypeId}
          onChange={(event) => setReportTypeId(event.target.value)}
          className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          <option value="">{t("selectPlaceholder")}</option>
          {reportTypes.map((entry) => (
            <option key={entry.id} value={entry.id}>
              {entry.name}
            </option>
          ))}
        </select>
      </div>

      {!reportType ? null : (
        <div className="mt-6">
          {schedules.isPending ? (
            <p className="text-sm text-ink-muted">{t("loading")}</p>
          ) : schedules.isError ? (
            <p className="text-sm text-danger">{schedules.error.message}</p>
          ) : (
            <ScheduleList reportType={reportType} schedules={schedules.data} />
          )}
        </div>
      )}
    </div>
  );
}

function ScheduleList({ reportType, schedules }: { reportType: ReportTypeSummary; schedules: WindowScheduleSummary[] }) {
  const t = useTranslations("Windows");
  const queryClient = useQueryClient();
  const activate = useActivateWindowSchedule<ApiError>();
  const deactivate = useDeactivateWindowSchedule<ApiError>();
  const generate = useGenerateScheduledWindows<ApiError>();
  const [isAdding, setIsAdding] = useState(false);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: getGetWindowSchedulesQueryKey(reportType.id) });
    queryClient.invalidateQueries({ queryKey: getGetSubmissionWindowsQueryKey(reportType.id) });
  };

  return (
    <div className="flex flex-col gap-4">
      {schedules.length === 0 ? (
        <p className="text-sm text-ink-muted">{t("noSchedulesYet")}</p>
      ) : (
        <div className="flex flex-col gap-3">
          {schedules.map((schedule) => (
            <div key={schedule.id} className="rounded-md border border-line p-3">
              <div className="flex items-center justify-between gap-3">
                <p className="font-medium text-ink">
                  {frequencyLabel(schedule.frequency)} · {schedule.isActive ? t("statusActive") : t("statusInactive")}
                </p>
                <div className="flex gap-2">
                  {schedule.isActive ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={deactivate.isPending}
                      onClick={() => deactivate.mutate({ reportTypeId: reportType.id, scheduleId: schedule.id }, { onSuccess: invalidate })}
                    >
                      {t("deactivate")}
                    </Button>
                  ) : (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={activate.isPending}
                      onClick={() => activate.mutate({ reportTypeId: reportType.id, scheduleId: schedule.id }, { onSuccess: invalidate })}
                    >
                      {t("activate")}
                    </Button>
                  )}
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    disabled={generate.isPending}
                    onClick={() => generate.mutate({ reportTypeId: reportType.id, scheduleId: schedule.id }, { onSuccess: invalidate })}
                  >
                    {t("generateNow")}
                  </Button>
                </div>
              </div>
              <p className="mt-1 text-xs text-ink-muted">
                {t("scheduleSummary", {
                  opens: Number(schedule.opensDayOffset),
                  grace: Number(schedule.graceDays),
                  closes: Number(schedule.closesDayOffset),
                })}
              </p>
            </div>
          ))}
        </div>
      )}

      {isAdding ? (
        <NewScheduleForm reportType={reportType} onDone={() => setIsAdding(false)} onCreated={invalidate} />
      ) : (
        <Button type="button" size="sm" variant="outline" onClick={() => setIsAdding(true)}>
          <Plus className="size-4" aria-hidden="true" />
          {t("addSchedule")}
        </Button>
      )}
    </div>
  );
}

function NewScheduleForm({
  reportType,
  onDone,
  onCreated,
}: {
  reportType: ReportTypeSummary;
  onDone: () => void;
  onCreated: () => void;
}) {
  const t = useTranslations("Windows");
  const createSchedule = useCreateWindowSchedule<ApiError>();

  const applicableLevels = DUE_OFFSET_LEVEL_ORDER.filter((level) => reportType.applicableLevels.includes(level));

  const [frequency, setFrequency] = useState(String(SCHEDULE_FREQUENCIES[0]));
  const [opensDayOffset, setOpensDayOffset] = useState("1");
  const [graceDays, setGraceDays] = useState("3");
  const [closesDayOffset, setClosesDayOffset] = useState("25");
  const [dueDayOffsets, setDueDayOffsets] = useState<Record<number, string>>(
    Object.fromEntries(applicableLevels.map((level) => [level, "10"])),
  );

  const onAdd = () => {
    createSchedule.mutate(
      {
        reportTypeId: reportType.id,
        data: {
          frequency: Number(frequency),
          opensDayOffset: Number(opensDayOffset),
          graceDays: Number(graceDays),
          closesDayOffset: Number(closesDayOffset),
          dueOffsets: applicableLevels.map((level) => ({ level, dueDayOffset: Number(dueDayOffsets[level] ?? 0) })),
        },
      },
      {
        onSuccess: () => {
          onCreated();
          onDone();
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-3 rounded-md border border-line bg-ground p-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="new-schedule-frequency">{t("frequencyLabel")}</Label>
        <select
          id="new-schedule-frequency"
          value={frequency}
          onChange={(event) => setFrequency(event.target.value)}
          className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          {SCHEDULE_FREQUENCIES.map((value) => (
            <option key={value} value={value}>
              {frequencyLabel(value)}
            </option>
          ))}
        </select>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-schedule-opens">{t("opensDayOffsetLabel")}</Label>
          <Input id="new-schedule-opens" inputMode="numeric" value={opensDayOffset} onChange={(event) => setOpensDayOffset(event.target.value)} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-schedule-grace">{t("graceLabel")}</Label>
          <Input id="new-schedule-grace" inputMode="numeric" value={graceDays} onChange={(event) => setGraceDays(event.target.value)} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-schedule-closes">{t("closesDayOffsetLabel")}</Label>
          <Input id="new-schedule-closes" inputMode="numeric" value={closesDayOffset} onChange={(event) => setClosesDayOffset(event.target.value)} />
        </div>
      </div>

      {applicableLevels.length === 0 ? null : (
        <div className="flex flex-col gap-2">
          <Label>{t("dueDayOffsetsLabel")}</Label>
          {applicableLevels.map((level) => (
            <div key={level} className="flex items-center gap-2">
              <span className="w-20 shrink-0 text-sm text-ink">{levelLabel(level)}</span>
              <Input
                inputMode="numeric"
                value={dueDayOffsets[level] ?? ""}
                onChange={(event) => setDueDayOffsets((current) => ({ ...current, [level]: event.target.value }))}
                aria-label={t("dueDayOffsetForLevel", { level: levelLabel(level) })}
              />
            </div>
          ))}
        </div>
      )}

      {createSchedule.isError ? <p className="text-xs text-danger">{createSchedule.error.message}</p> : null}

      <div className="flex gap-2">
        <Button type="button" size="sm" onClick={onAdd} disabled={createSchedule.isPending}>
          {createSchedule.isPending ? t("saving") : t("save")}
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={onDone}>
          {t("cancel")}
        </Button>
      </div>
    </div>
  );
}
