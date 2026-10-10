"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { reportTypeFormSchema, type ReportTypeFormValues } from "@/features/templates/schemas/report-type-form";
import {
  categoryLabel,
  frequencyLabel,
  levelLabel,
  REPORT_TYPE_CATEGORY,
  REPORT_TYPE_FREQUENCY,
  UNIT_LEVEL_CODE,
} from "@/features/templates/utils/report-type";

const SUBMITTABLE_LEVELS = [UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Department];
const ALL_LEVELS = [UNIT_LEVEL_CODE.National, ...SUBMITTABLE_LEVELS];

export interface ReportTypeFormProps {
  defaultValues: ReportTypeFormValues;
  onSubmit: (values: ReportTypeFormValues) => void;
  onCancel: () => void;
  submitLabel: string;
  pendingLabel: string;
  isPending: boolean;
  errorMessage?: string;
  disabled?: boolean;
}

export function ReportTypeForm({
  defaultValues,
  onSubmit,
  onCancel,
  submitLabel,
  pendingLabel,
  isPending,
  errorMessage,
  disabled,
}: ReportTypeFormProps) {
  const t = useTranslations("ReportTypes");
  const form = useForm<ReportTypeFormValues>({
    resolver: zodResolver(reportTypeFormSchema),
    defaultValues,
  });

  const applicableLevels = useWatch({ control: form.control, name: "applicableLevels" });
  const allowAttachments = useWatch({ control: form.control, name: "allowAttachments" });
  const allowNilReport = useWatch({ control: form.control, name: "allowNilReport" });

  const toggleLevel = (level: number, checked: boolean) => {
    const value = String(level);
    const next = checked ? [...applicableLevels, value] : applicableLevels.filter((entry) => entry !== value);
    form.setValue("applicableLevels", next, { shouldValidate: true });
  };

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="report-type-name">{t("nameLabel")}</Label>
        <Input id="report-type-name" {...form.register("name")} aria-invalid={!!form.formState.errors.name} disabled={disabled} />
        {form.formState.errors.name ? <p className="text-xs text-danger">{form.formState.errors.name.message}</p> : null}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="report-type-description">{t("descriptionLabel")}</Label>
        <Input id="report-type-description" {...form.register("description")} disabled={disabled} />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="report-type-category">{t("categoryLabel")}</Label>
          <select
            id="report-type-category"
            {...form.register("category")}
            disabled={disabled}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("selectPlaceholder")}</option>
            {Object.values(REPORT_TYPE_CATEGORY).map((code) => (
              <option key={code} value={code}>
                {categoryLabel(code)}
              </option>
            ))}
          </select>
          {form.formState.errors.category ? (
            <p className="text-xs text-danger">{form.formState.errors.category.message}</p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="report-type-frequency">{t("frequencyLabel")}</Label>
          <select
            id="report-type-frequency"
            {...form.register("frequency")}
            disabled={disabled}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("selectPlaceholder")}</option>
            {Object.values(REPORT_TYPE_FREQUENCY).map((code) => (
              <option key={code} value={code}>
                {frequencyLabel(code)}
              </option>
            ))}
          </select>
          {form.formState.errors.frequency ? (
            <p className="text-xs text-danger">{form.formState.errors.frequency.message}</p>
          ) : null}
        </div>
      </div>

      <fieldset className="flex flex-col gap-1.5">
        <legend className="text-sm font-medium text-ink">{t("whoSubmitsLabel")}</legend>
        <div className="flex flex-wrap gap-x-4 gap-y-2">
          {SUBMITTABLE_LEVELS.map((level) => (
            <label key={level} className="flex items-center gap-2 text-sm text-ink">
              <Checkbox
                checked={applicableLevels.includes(String(level))}
                onCheckedChange={(checked) => toggleLevel(level, checked === true)}
                disabled={disabled}
              />
              {levelLabel(level)}
            </label>
          ))}
        </div>
        {form.formState.errors.applicableLevels ? (
          <p className="text-xs text-danger">{form.formState.errors.applicableLevels.message}</p>
        ) : null}
      </fieldset>

      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="report-type-recipient">{t("recipientLevelLabel")}</Label>
          <select
            id="report-type-recipient"
            {...form.register("recipientLevel")}
            disabled={disabled}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            {ALL_LEVELS.map((level) => (
              <option key={level} value={level}>
                {levelLabel(level)}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="report-type-observer-depth">{t("observerDepthLabel")}</Label>
          <Input
            id="report-type-observer-depth"
            inputMode="numeric"
            {...form.register("observerDepth")}
            aria-invalid={!!form.formState.errors.observerDepth}
            disabled={disabled}
          />
          {form.formState.errors.observerDepth ? (
            <p className="text-xs text-danger">{form.formState.errors.observerDepth.message}</p>
          ) : null}
        </div>
      </div>

      <label className="flex items-center gap-2 text-sm text-ink">
        <Checkbox
          checked={allowAttachments}
          onCheckedChange={(checked) => form.setValue("allowAttachments", checked === true)}
          disabled={disabled}
        />
        {t("allowAttachmentsLabel")}
      </label>

      <label className="flex items-center gap-2 text-sm text-ink">
        <Checkbox
          checked={allowNilReport}
          onCheckedChange={(checked) => form.setValue("allowNilReport", checked === true)}
          disabled={disabled}
        />
        {t("allowNilReportLabel")}
      </label>

      {errorMessage ? (
        <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
          {errorMessage}
        </p>
      ) : null}

      <div className="flex gap-2">
        <Button type="submit" disabled={isPending || disabled}>
          {isPending ? pendingLabel : submitLabel}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel}>
          {t("cancel")}
        </Button>
      </div>
    </form>
  );
}
