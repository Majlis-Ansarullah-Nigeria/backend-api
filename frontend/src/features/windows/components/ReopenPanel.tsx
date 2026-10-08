"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { WindowReopenSummary } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetSubmissionWindowQueryKey, useReopenWindow } from "@/lib/api/generated/windows/windows";
import type { UnitOption } from "./ExtensionsPanel";

export function ReopenPanel({
  reportTypeId,
  windowId,
  reopens,
  units,
}: {
  reportTypeId: string;
  windowId: string;
  reopens: WindowReopenSummary[];
  units: UnitOption[];
}) {
  const t = useTranslations("Windows");
  const queryClient = useQueryClient();
  const reopenWindow = useReopenWindow<ApiError>();

  const [isAdding, setIsAdding] = useState(false);
  const [unitId, setUnitId] = useState("");
  const [reopenedUntilDate, setReopenedUntilDate] = useState("");
  const [reason, setReason] = useState("");

  const unitName = (id: string) => units.find((unit) => unit.id === id)?.name ?? id;

  const resetForm = () => {
    setIsAdding(false);
    setUnitId("");
    setReopenedUntilDate("");
    setReason("");
  };

  const onAdd = () => {
    if (!unitId || !reopenedUntilDate || !reason.trim()) {
      return;
    }

    reopenWindow.mutate(
      {
        reportTypeId,
        windowId,
        data: {
          unitIds: [unitId],
          reopenedUntil: new Date(`${reopenedUntilDate}T23:59:00`).toISOString(),
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
        <h2 className="text-lg font-bold text-ink">{t("reopenTitle")}</h2>
        <Button type="button" size="sm" variant="outline" onClick={() => setIsAdding((value) => !value)}>
          <Plus className="size-4" aria-hidden="true" />
          {t("add")}
        </Button>
      </div>
      <p className="mt-1 text-sm text-ink-muted">{t("reopenSubtitle")}</p>

      {isAdding ? (
        <div className="mt-4 flex flex-col gap-3 rounded-md border border-line bg-ground p-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="reopen-unit">{t("unitLabel")}</Label>
            <select
              id="reopen-unit"
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

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="reopen-until">{t("reopenedUntilLabel")}</Label>
            <Input
              id="reopen-until"
              type="date"
              value={reopenedUntilDate}
              onChange={(event) => setReopenedUntilDate(event.target.value)}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="reopen-reason">{t("reasonLabel")}</Label>
            <Input id="reopen-reason" value={reason} onChange={(event) => setReason(event.target.value)} />
          </div>

          {reopenWindow.isError ? <p className="text-xs text-danger">{reopenWindow.error.message}</p> : null}

          <div className="flex gap-2">
            <Button type="button" size="sm" onClick={onAdd} disabled={reopenWindow.isPending}>
              {reopenWindow.isPending ? t("saving") : t("save")}
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={resetForm}>
              {t("cancel")}
            </Button>
          </div>
        </div>
      ) : null}

      <div className="mt-4 flex flex-col gap-3">
        {reopens.length === 0 ? (
          <p className="text-sm text-ink-muted">{t("noReopensYet")}</p>
        ) : (
          reopens.map((reopen, index) => (
            <div key={`${reopen.unitId}-${reopen.reopenedAt}-${index}`} className="rounded-md border border-line p-3">
              <p className="font-medium text-ink">{unitName(reopen.unitId)}</p>
              <p className="mt-1 text-xs text-ink-muted">{reopen.reason}</p>
              <p className="mt-1 text-xs text-ink-muted">
                {t("reopenedUntilOn", { date: new Date(reopen.reopenedUntil).toLocaleDateString() })}
              </p>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
