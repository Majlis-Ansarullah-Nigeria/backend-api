"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { UnitLevelSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetOrganizationTreeQueryKey, useCreateUnit } from "@/lib/api/generated/organization/organization";
import { createUnitSchema, type CreateUnitFormValues } from "@/features/organization/schemas/unit-form";
import { UNIT_LEVEL_CODE } from "@/features/organization/utils/tree";

export interface AddUnitPanelProps {
  levels: UnitLevelSummary[];
  units: UnitTreeNode[];
  defaultParentId: string | null;
  onCancel: () => void;
  onCreated: (unitId: string) => void;
}

export function AddUnitPanel({ levels, units, defaultParentId, onCancel, onCreated }: AddUnitPanelProps) {
  const t = useTranslations("Organization");
  const queryClient = useQueryClient();
  const createUnit = useCreateUnit<ApiError>();

  const form = useForm<CreateUnitFormValues>({
    resolver: zodResolver(createUnitSchema),
    defaultValues: {
      code: "",
      name: "",
      levelId: "",
      parentId: defaultParentId ?? "",
      address: "",
      contactPhone: "",
      contactEmail: "",
      latitude: "",
      longitude: "",
    },
  });

  const candidateParents = units
    .filter((unit) => unit.archivedAt === null)
    .slice()
    .sort((a, b) => a.path.localeCompare(b.path));

  const onSubmit = (values: CreateUnitFormValues) => {
    createUnit.mutate(
      {
        data: {
          code: values.code,
          name: values.name,
          levelId: values.levelId,
          parentId: values.parentId,
          address: values.address ? values.address : null,
          contactPhone: values.contactPhone ? values.contactPhone : null,
          contactEmail: values.contactEmail ? values.contactEmail : null,
          latitude: values.latitude ? Number(values.latitude) : null,
          longitude: values.longitude ? Number(values.longitude) : null,
        },
      },
      {
        onSuccess: (created) => {
          queryClient.invalidateQueries({ queryKey: getGetOrganizationTreeQueryKey() });
          onCreated(created.id);
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <h2 className="text-lg font-bold text-ink">{t("addUnit")}</h2>

      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-unit-level">{t("levelLabel")}</Label>
          <select
            id="new-unit-level"
            {...form.register("levelId")}
            aria-invalid={!!form.formState.errors.levelId}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("selectPlaceholder")}</option>
            {levels
              .filter((level) => level.code !== UNIT_LEVEL_CODE.National)
              .map((level) => (
                <option key={level.id} value={level.id}>
                  {level.name}
                </option>
              ))}
          </select>
          {form.formState.errors.levelId ? (
            <p className="text-xs text-danger">{form.formState.errors.levelId.message}</p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-unit-parent">{t("parentLabel")}</Label>
          <select
            id="new-unit-parent"
            {...form.register("parentId")}
            aria-invalid={!!form.formState.errors.parentId}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("selectPlaceholder")}</option>
            {candidateParents.map((unit) => (
              <option key={unit.id} value={unit.id}>
                {unit.displayCode} — {unit.name}
              </option>
            ))}
          </select>
          {form.formState.errors.parentId ? (
            <p className="text-xs text-danger">{form.formState.errors.parentId.message}</p>
          ) : null}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="new-unit-code">{t("codeLabel")}</Label>
            <Input id="new-unit-code" {...form.register("code")} aria-invalid={!!form.formState.errors.code} />
            {form.formState.errors.code ? (
              <p className="text-xs text-danger">{form.formState.errors.code.message}</p>
            ) : null}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="new-unit-name">{t("nameLabel")}</Label>
            <Input id="new-unit-name" {...form.register("name")} aria-invalid={!!form.formState.errors.name} />
            {form.formState.errors.name ? (
              <p className="text-xs text-danger">{form.formState.errors.name.message}</p>
            ) : null}
          </div>
        </div>

        {createUnit.isError ? (
          <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
            {createUnit.error.message}
          </p>
        ) : null}

        <div className="flex gap-2">
          <Button type="submit" disabled={createUnit.isPending}>
            {createUnit.isPending ? t("creating") : t("create")}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel}>
            {t("cancel")}
          </Button>
        </div>
      </form>
    </div>
  );
}
