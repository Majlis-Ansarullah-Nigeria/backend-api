"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { TriangleAlert } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { UNIT_LEVEL_CODE, levelLabel } from "@/features/templates/utils/report-type";
import { detectParentBeforeChildWarnings } from "@/features/windows/utils/warnings";
import { windowFormSchema, type WindowFormValues } from "@/features/windows/schemas/window-form";
import type { ReportTypeSummary } from "@/lib/api/generated/hRSApiV1.schemas";

const DUE_DATE_LEVEL_ORDER = [UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Department];

export interface WindowFormProps {
  reportTypes: ReportTypeSummary[];
  unitCountsByLevel: Record<number, number>;
  defaultValues: WindowFormValues;
  reportTypeLocked: boolean;
  onSubmit: (values: WindowFormValues) => void;
  onCancel: () => void;
  submitLabel: string;
  pendingLabel: string;
  isPending: boolean;
  errorMessage?: string;
  serverWarnings: string[];
}

export function WindowForm({
  reportTypes,
  unitCountsByLevel,
  defaultValues,
  reportTypeLocked,
  onSubmit,
  onCancel,
  submitLabel,
  pendingLabel,
  isPending,
  errorMessage,
  serverWarnings,
}: WindowFormProps) {
  const t = useTranslations("Windows");
  const form = useForm<WindowFormValues>({ resolver: zodResolver(windowFormSchema), defaultValues });
  const { fields, replace } = useFieldArray({ control: form.control, name: "dueDates" });

  const reportTypeId = useWatch({ control: form.control, name: "reportTypeId" });
  const dueDates = useWatch({ control: form.control, name: "dueDates" });
  const reminderSettings = useWatch({ control: form.control, name: "reminderSettings" });

  const selectedReportType = reportTypes.find((reportType) => reportType.id === reportTypeId);

  useEffect(() => {
    if (!selectedReportType) {
      return;
    }

    const existingByLevel = new Map(form.getValues("dueDates").map((row) => [Number(row.level), row]));
    const orderedLevels = DUE_DATE_LEVEL_ORDER.filter((level) => selectedReportType.applicableLevels.includes(level));

    replace(
      orderedLevels.map(
        (level) => existingByLevel.get(level) ?? { level: String(level), dueDate: "", dueTime: "23:59" },
      ),
    );
    // Only resync rows when the selected report type itself changes, not on every row edit.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedReportType?.id]);

  const liveWarnings = detectParentBeforeChildWarnings(
    dueDates
      .filter((row) => row.dueDate)
      .map((row) => ({ level: Number(row.level), dueAt: `${row.dueDate}T${row.dueTime || "00:00"}:00` })),
  );
  const extraServerWarnings = serverWarnings.filter((warning) => !liveWarnings.includes(warning));

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]" noValidate>
      <div className="flex flex-col gap-6">
        <div className="rounded-lg border border-line bg-surface p-6">
          <h2 className="text-lg font-bold text-ink">{t("periodAndTemplateTitle")}</h2>

          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="window-report-type">{t("reportTypeLabel")}</Label>
              <select
                id="window-report-type"
                {...form.register("reportTypeId")}
                disabled={reportTypeLocked}
                aria-invalid={!!form.formState.errors.reportTypeId}
                className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40 disabled:opacity-60"
              >
                <option value="">{t("selectPlaceholder")}</option>
                {reportTypes.map((reportType) => (
                  <option key={reportType.id} value={reportType.id}>
                    {reportType.name}
                  </option>
                ))}
              </select>
              {form.formState.errors.reportTypeId ? (
                <p className="text-xs text-danger">{form.formState.errors.reportTypeId.message}</p>
              ) : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="window-period-label">{t("periodLabelLabel")}</Label>
              <Input
                id="window-period-label"
                {...form.register("periodLabel")}
                aria-invalid={!!form.formState.errors.periodLabel}
              />
              {form.formState.errors.periodLabel ? (
                <p className="text-xs text-danger">{form.formState.errors.periodLabel.message}</p>
              ) : null}
            </div>
          </div>

          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="window-opens">{t("opensLabel")}</Label>
              <Input
                id="window-opens"
                type="date"
                {...form.register("opensDate")}
                aria-invalid={!!form.formState.errors.opensDate}
              />
              {form.formState.errors.opensDate ? (
                <p className="text-xs text-danger">{form.formState.errors.opensDate.message}</p>
              ) : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="window-grace">{t("graceLabel")}</Label>
              <div className="flex items-center gap-2">
                <Input
                  id="window-grace"
                  inputMode="numeric"
                  {...form.register("graceDays")}
                  aria-invalid={!!form.formState.errors.graceDays}
                />
                <span className="shrink-0 text-sm text-ink-muted">{t("daysSuffix")}</span>
              </div>
              <p className="text-xs text-ink-muted">{t("graceHint")}</p>
              {form.formState.errors.graceDays ? (
                <p className="text-xs text-danger">{form.formState.errors.graceDays.message}</p>
              ) : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="window-closes">{t("closesLabel")}</Label>
              <Input
                id="window-closes"
                type="date"
                {...form.register("closesDate")}
                aria-invalid={!!form.formState.errors.closesDate}
              />
              {form.formState.errors.closesDate ? (
                <p className="text-xs text-danger">{form.formState.errors.closesDate.message}</p>
              ) : null}
            </div>
          </div>

          {selectedReportType ? (
            <p className="mt-4 text-sm text-ink-muted">
              {t("whoMustSubmitLabel")}:{" "}
              {DUE_DATE_LEVEL_ORDER.filter((level) => selectedReportType.applicableLevels.includes(level))
                .map(levelLabel)
                .join(", ")}
            </p>
          ) : null}
        </div>

        <div className="rounded-lg border border-line bg-surface p-6">
          <h2 className="text-lg font-bold text-ink">{t("dueDatesTitle")}</h2>
          <p className="mt-1 text-sm text-ink-muted">{t("dueDatesSubtitle")}</p>

          {!selectedReportType ? (
            <p className="mt-4 text-sm text-ink-muted">{t("chooseReportTypeFirst")}</p>
          ) : (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-muted">
                    <th scope="col" className="py-2 pr-3 font-medium">
                      {t("columnLevel")}
                    </th>
                    <th scope="col" className="py-2 pr-3 font-medium">
                      {t("columnDueDate")}
                    </th>
                    <th scope="col" className="py-2 pr-3 font-medium">
                      {t("columnTime")}
                    </th>
                    <th scope="col" className="py-2 font-medium">
                      {t("columnUnits")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id} className="border-b border-line last:border-0">
                      <td className="py-2 pr-3 font-medium text-ink">{levelLabel(Number(field.level))}</td>
                      <td className="py-2 pr-3">
                        <Input type="date" {...form.register(`dueDates.${index}.dueDate`)} />
                      </td>
                      <td className="py-2 pr-3">
                        <Input type="time" {...form.register(`dueDates.${index}.dueTime`)} />
                      </td>
                      <td className="py-2 text-ink-muted">
                        {t("unitsCount", { count: unitCountsByLevel[Number(field.level)] ?? 0 })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {form.formState.errors.dueDates?.message ? (
                <p className="mt-2 text-xs text-danger">{form.formState.errors.dueDates.message}</p>
              ) : null}
            </div>
          )}

          {[...liveWarnings, ...extraServerWarnings].map((warning) => (
            <div
              key={warning}
              role="alert"
              className="mt-4 flex items-start gap-2 rounded-md bg-status-upcoming-bg px-3 py-2 text-sm text-status-upcoming-fg"
            >
              <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <span>{warning}</span>
            </div>
          ))}
        </div>

        {errorMessage ? (
          <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
            {errorMessage}
          </p>
        ) : null}

        <div className="flex gap-2">
          <Button type="submit" disabled={isPending}>
            {isPending ? pendingLabel : submitLabel}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel}>
            {t("cancel")}
          </Button>
        </div>
      </div>

      <div className="rounded-lg border border-line bg-surface p-6">
        <h2 className="text-lg font-bold text-ink">{t("remindersTitle")}</h2>
        <div className="mt-4 flex flex-col gap-4">
          <ReminderToggle
            label={t("remindFiveDaysBefore")}
            checked={reminderSettings.remindFiveDaysBefore}
            onCheckedChange={(checked) => form.setValue("reminderSettings.remindFiveDaysBefore", checked)}
          />
          <ReminderToggle
            label={t("remindTwoDaysBefore")}
            checked={reminderSettings.remindTwoDaysBefore}
            onCheckedChange={(checked) => form.setValue("reminderSettings.remindTwoDaysBefore", checked)}
          />
          <ReminderToggle
            label={t("remindOnDueDay")}
            checked={reminderSettings.remindOnDueDay}
            onCheckedChange={(checked) => form.setValue("reminderSettings.remindOnDueDay", checked)}
          />
          <ReminderToggle
            label={t("escalateToParentAfterThreeDays")}
            checked={reminderSettings.escalateToParentAfterThreeDays}
            onCheckedChange={(checked) => form.setValue("reminderSettings.escalateToParentAfterThreeDays", checked)}
          />
          <ReminderToggle
            label={t("escalateToZoneAfterSevenDays")}
            checked={reminderSettings.escalateToZoneAfterSevenDays}
            onCheckedChange={(checked) => form.setValue("reminderSettings.escalateToZoneAfterSevenDays", checked)}
          />
        </div>
      </div>
    </form>
  );
}

function ReminderToggle({
  label,
  checked,
  onCheckedChange,
}: {
  label: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex items-center justify-between gap-3 text-sm text-ink">
      <span>{label}</span>
      <Switch checked={checked} onCheckedChange={onCheckedChange} />
    </label>
  );
}
