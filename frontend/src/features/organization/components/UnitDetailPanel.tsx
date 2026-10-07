"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { useForm } from "react-hook-form";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { UnitSummary, UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetOrganizationTreeQueryKey,
  getGetUnitQueryKey,
  useArchiveUnit,
  useGetUnit,
  useRestoreUnit,
  useUpdateUnit,
} from "@/lib/api/generated/organization/organization";
import { updateUnitSchema, type UpdateUnitFormValues } from "@/features/organization/schemas/unit-form";
import { AssignHeadPanel } from "./AssignHeadPanel";

export interface UnitDetailPanelProps {
  unitId: string;
  breadcrumb: UnitTreeNode[];
  levelName?: string;
  node: UnitTreeNode;
}

export function UnitDetailPanel({ unitId, breadcrumb, levelName, node }: UnitDetailPanelProps) {
  const t = useTranslations("Organization");
  const unit = useGetUnit<UnitSummary, ApiError>(unitId);

  if (unit.isPending) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6 text-sm text-ink-muted">{t("detailLoading")}</div>
    );
  }

  if (unit.isError) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6">
        <ErrorState title={t("detailErrorTitle")} onRetry={() => unit.refetch()} />
      </div>
    );
  }

  return <UnitDetailForm key={unit.data.id} unit={unit.data} breadcrumb={breadcrumb} levelName={levelName} node={node} />;
}

function UnitDetailForm({
  unit,
  breadcrumb,
  levelName,
  node,
}: {
  unit: UnitSummary;
  breadcrumb: UnitTreeNode[];
  levelName?: string;
  node: UnitTreeNode;
}) {
  const t = useTranslations("Organization");
  const queryClient = useQueryClient();
  const updateUnit = useUpdateUnit<ApiError>();
  const archiveUnit = useArchiveUnit<ApiError>();
  const restoreUnit = useRestoreUnit<ApiError>();

  const form = useForm<UpdateUnitFormValues>({
    resolver: zodResolver(updateUnitSchema),
    defaultValues: {
      name: unit.name,
      address: unit.address ?? "",
      contactPhone: unit.contactPhone ?? "",
      contactEmail: unit.contactEmail ?? "",
      latitude: unit.latitude != null ? String(unit.latitude) : "",
      longitude: unit.longitude != null ? String(unit.longitude) : "",
    },
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: getGetUnitQueryKey(unit.id) });
    queryClient.invalidateQueries({ queryKey: getGetOrganizationTreeQueryKey() });
  };

  const onSubmit = (values: UpdateUnitFormValues) => {
    updateUnit.mutate(
      {
        id: unit.id,
        data: {
          name: values.name,
          address: values.address ? values.address : null,
          contactPhone: values.contactPhone ? values.contactPhone : null,
          contactEmail: values.contactEmail ? values.contactEmail : null,
          latitude: values.latitude ? Number(values.latitude) : null,
          longitude: values.longitude ? Number(values.longitude) : null,
        },
      },
      { onSuccess: invalidate },
    );
  };

  const isRoot = unit.parentId === null;

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div>
        <p className="font-mono text-xs uppercase tracking-wide text-ink-muted">
          {breadcrumb.map((node) => node.name).join(" / ")}
        </p>
        <h2 className="mt-1 text-lg font-bold text-ink">{unit.name}</h2>
        <p className="mt-1 text-sm text-ink-muted">
          {levelName} · <span className="font-mono">{unit.displayCode}</span>
        </p>
        {unit.archivedAt ? (
          <p className="mt-2 inline-flex rounded-full bg-status-missed-bg px-2 py-0.5 text-xs font-medium text-status-missed-fg">
            {t("archived")}
          </p>
        ) : null}
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        <Field id="unit-name" label={t("nameLabel")} error={form.formState.errors.name?.message}>
          <Input
            id="unit-name"
            {...form.register("name")}
            aria-invalid={!!form.formState.errors.name}
            disabled={!!unit.archivedAt}
          />
        </Field>

        <div className="grid grid-cols-2 gap-3">
          <Field id="unit-code" label={t("codeLabel")}>
            <Input id="unit-code" value={unit.code} disabled readOnly />
          </Field>
          <Field id="unit-parent" label={t("parentLabel")}>
            <Input
              id="unit-parent"
              value={breadcrumb.length > 1 ? breadcrumb[breadcrumb.length - 2]!.name : "—"}
              disabled
              readOnly
            />
          </Field>
        </div>

        <Field id="unit-address" label={t("addressLabel")} error={form.formState.errors.address?.message}>
          <Input id="unit-address" {...form.register("address")} disabled={!!unit.archivedAt} />
        </Field>

        <div className="grid grid-cols-2 gap-3">
          <Field id="unit-phone" label={t("phoneLabel")} error={form.formState.errors.contactPhone?.message}>
            <Input id="unit-phone" type="tel" {...form.register("contactPhone")} disabled={!!unit.archivedAt} />
          </Field>
          <Field id="unit-email" label={t("emailLabel")} error={form.formState.errors.contactEmail?.message}>
            <Input id="unit-email" type="email" {...form.register("contactEmail")} disabled={!!unit.archivedAt} />
          </Field>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Field id="unit-lat" label={t("latitudeLabel")} error={form.formState.errors.latitude?.message}>
            <Input id="unit-lat" inputMode="decimal" {...form.register("latitude")} disabled={!!unit.archivedAt} />
          </Field>
          <Field id="unit-lng" label={t("longitudeLabel")} error={form.formState.errors.longitude?.message}>
            <Input id="unit-lng" inputMode="decimal" {...form.register("longitude")} disabled={!!unit.archivedAt} />
          </Field>
        </div>

        {updateUnit.isError ? (
          <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
            {updateUnit.error.message}
          </p>
        ) : null}

        {!unit.archivedAt ? (
          <Button type="submit" disabled={updateUnit.isPending}>
            {updateUnit.isPending ? t("saving") : t("save")}
          </Button>
        ) : null}
      </form>

      {!unit.archivedAt ? (
        <AssignHeadPanel unitId={unit.id} unitName={unit.name} headName={node.headName} hasVacantHead={node.hasVacantHead} />
      ) : null}

      {archiveUnit.isError ? (
        <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
          {archiveUnit.error.message}
        </p>
      ) : null}

      {unit.archivedAt ? (
        <Button
          type="button"
          variant="outline"
          disabled={restoreUnit.isPending}
          onClick={() => restoreUnit.mutate({ id: unit.id }, { onSuccess: invalidate })}
        >
          {restoreUnit.isPending ? t("restoring") : t("restore")}
        </Button>
      ) : isRoot ? null : (
        <Button
          type="button"
          variant="destructive"
          disabled={archiveUnit.isPending}
          onClick={() => archiveUnit.mutate({ id: unit.id }, { onSuccess: invalidate })}
        >
          {archiveUnit.isPending ? t("archiving") : t("archive")}
        </Button>
      )}
    </div>
  );
}

function Field({ id, label, error, children }: { id: string; label: string; error?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? <p className="text-xs text-danger">{error}</p> : null}
    </div>
  );
}
