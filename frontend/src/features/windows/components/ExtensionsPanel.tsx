"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { WindowExtensionSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetSubmissionWindowQueryKey, useGrantWindowExtension } from "@/lib/api/generated/windows/windows";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

export interface UnitOption {
  id: string;
  name: string;
}

export function ExtensionsPanel({
  reportTypeId,
  windowId,
  extensions,
  units,
}: {
  reportTypeId: string;
  windowId: string;
  extensions: WindowExtensionSummary[];
  units: UnitOption[];
}) {
  const fmt = useOrgFormat();
  const t = useTranslations("Windows");
  const queryClient = useQueryClient();
  const grantExtension = useGrantWindowExtension<ApiError>();

  const [isAdding, setIsAdding] = useState(false);
  const [unitId, setUnitId] = useState("");
  const [newDueDate, setNewDueDate] = useState("");
  const [newClosesDate, setNewClosesDate] = useState("");
  const [reason, setReason] = useState("");

  const unitName = (id: string) => units.find((unit) => unit.id === id)?.name ?? id;

  const resetForm = () => {
    setIsAdding(false);
    setUnitId("");
    setNewDueDate("");
    setNewClosesDate("");
    setReason("");
  };

  const onAdd = () => {
    if (!unitId || !newDueDate || !newClosesDate || !reason.trim()) {
      return;
    }

    grantExtension.mutate(
      {
        reportTypeId,
        windowId,
        data: {
          unitIds: [unitId],
          newDueAt: new Date(`${newDueDate}T23:59:00`).toISOString(),
          newClosesAt: new Date(`${newClosesDate}T23:59:00`).toISOString(),
          reason: reason.trim(),
        },
      },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: getGetSubmissionWindowQueryKey(reportTypeId, windowId) });
          resetForm();
        },
      },
    );
  };

  return (
    <div className="rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-bold text-ink">{t("extensionsTitle")}</h2>
        <Button type="button" size="sm" variant="outline" onClick={() => setIsAdding((value) => !value)}>
          <Plus className="size-4" aria-hidden="true" />
          {t("add")}
        </Button>
      </div>

      {isAdding ? (
        <div className="mt-4 flex flex-col gap-3 rounded-md border border-line bg-ground p-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="extension-unit">{t("unitLabel")}</Label>
            <select
              id="extension-unit"
              value={unitId}
              onChange={(event) => setUnitId(event.target.value)}
              className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            >
              <option value="">{t("selectPlaceholder")}</option>
              {units.map((unit) => (
                <option key={unit.id} value={unit.id}>
                  {unit.name}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="extension-due">{t("newDueDateLabel")}</Label>
              <Input id="extension-due" type="date" value={newDueDate} onChange={(event) => setNewDueDate(event.target.value)} />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="extension-closes">{t("newClosesDateLabel")}</Label>
              <Input
                id="extension-closes"
                type="date"
                value={newClosesDate}
                onChange={(event) => setNewClosesDate(event.target.value)}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="extension-reason">{t("reasonLabel")}</Label>
            <Input id="extension-reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          </div>

          {grantExtension.isError ? <p className="text-xs text-danger">{grantExtension.error.message}</p> : null}

          <div className="flex gap-2">
            <Button type="button" size="sm" onClick={onAdd} disabled={grantExtension.isPending}>
              {grantExtension.isPending ? t("saving") : t("save")}
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={resetForm}>
              {t("cancel")}
            </Button>
          </div>
        </div>
      ) : null}

      <div className="mt-4 flex flex-col gap-3">
        {extensions.length === 0 ? (
          <p className="text-sm text-ink-muted">{t("noExtensionsYet")}</p>
        ) : (
          extensions.map((extension, index) => (
            <div key={`${extension.unitId}-${extension.grantedAt}-${index}`} className="rounded-md border border-line p-3">
              <p className="font-medium text-ink">{unitName(extension.unitId)}</p>
              <p className="mt-1 text-xs text-ink-muted">{extension.reason}</p>
              <p className="mt-1 text-xs text-ink-muted">
                {t("dueOn", { date: fmt.date(extension.newDueAt) })}
              </p>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
