"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ApiError } from "@/lib/api/client";
import { getGetReportTypesQueryKey, useCreateReportType } from "@/lib/api/generated/templates/templates";
import type { ReportTypeFormValues } from "@/features/templates/schemas/report-type-form";
import { ReportTypeForm } from "./ReportTypeForm";

const EMPTY_DEFAULTS: ReportTypeFormValues = {
  name: "",
  description: "",
  category: "",
  frequency: "",
  applicableLevels: [],
  recipientLevel: "0",
  observerDepth: "1",
  allowAttachments: false,
  allowNilReport: false,
};

export function CreateReportTypePanel({ onCancel, onCreated }: { onCancel: () => void; onCreated: (id: string) => void }) {
  const t = useTranslations("ReportTypes");
  const queryClient = useQueryClient();
  const createReportType = useCreateReportType<ApiError>();

  const onSubmit = (values: ReportTypeFormValues) => {
    createReportType.mutate(
      {
        data: {
          name: values.name,
          description: values.description ? values.description : null,
          category: Number(values.category),
          frequency: Number(values.frequency),
          applicableLevels: values.applicableLevels.map(Number),
          recipientLevel: Number(values.recipientLevel),
          observerDepth: Number(values.observerDepth),
          allowAttachments: values.allowAttachments,
          allowNilReport: values.allowNilReport,
        },
      },
      {
        onSuccess: (created) => {
          queryClient.invalidateQueries({ queryKey: getGetReportTypesQueryKey() });
          onCreated(created.id);
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <h2 className="text-lg font-bold text-ink">{t("newReportType")}</h2>
      <ReportTypeForm
        defaultValues={EMPTY_DEFAULTS}
        onSubmit={onSubmit}
        onCancel={onCancel}
        submitLabel={t("create")}
        pendingLabel={t("creating")}
        isPending={createReportType.isPending}
        errorMessage={createReportType.isError ? createReportType.error.message : undefined}
      />
    </div>
  );
}
