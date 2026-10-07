"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { UnitTreeNode } from "@/lib/api/generated/hRSApiV1.schemas";
import { getGetOrganizationTreeQueryKey, getGetUnitQueryKey, useMoveUnit } from "@/lib/api/generated/organization/organization";

export interface MoveUnitPanelProps {
  unitId: string;
  unitName: string;
  parentId: string | null;
  path: string;
  units: UnitTreeNode[];
}

export function MoveUnitPanel({ unitId, unitName, parentId, path, units }: MoveUnitPanelProps) {
  const t = useTranslations("Organization");
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [newParentId, setNewParentId] = useState("");
  const [moved, setMoved] = useState(false);
  const moveUnit = useMoveUnit<ApiError>();

  if (parentId === null) {
    return null;
  }

  const currentParent = units.find((unit) => unit.id === parentId);
  const candidateParents = units
    .filter(
      (unit) =>
        unit.archivedAt === null &&
        unit.levelCode === currentParent?.levelCode &&
        unit.id !== unitId &&
        unit.id !== parentId &&
        unit.path !== path &&
        !unit.path.startsWith(`${path}.`),
    )
    .slice()
    .sort((a, b) => a.path.localeCompare(b.path));

  const onConfirm = () => {
    if (!newParentId) {
      return;
    }
    moveUnit.mutate(
      { id: unitId, data: { newParentId } },
      {
        onSuccess: () => {
          setMoved(true);
          setIsOpen(false);
          setNewParentId("");
          queryClient.invalidateQueries({ queryKey: getGetOrganizationTreeQueryKey() });
          queryClient.invalidateQueries({ queryKey: getGetUnitQueryKey(unitId) });
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-3">
      {moved ? (
        <p className="rounded-md bg-status-acknowledged-bg px-3 py-2 text-sm text-status-acknowledged-fg">
          {t("unitMoved")}
        </p>
      ) : null}

      {!isOpen ? (
        <Button type="button" variant="outline" onClick={() => setIsOpen(true)}>
          {t("moveUnit")}
        </Button>
      ) : (
        <div className="flex flex-col gap-3 rounded-md bg-ground p-3">
          <p className="text-sm font-medium text-ink">{t("moveUnitTo", { unit: unitName })}</p>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="move-unit-new-parent">{t("newParentLabel")}</Label>
            <select
              id="move-unit-new-parent"
              value={newParentId}
              onChange={(event) => setNewParentId(event.target.value)}
              className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
            >
              <option value="">{t("selectPlaceholder")}</option>
              {candidateParents.map((unit) => (
                <option key={unit.id} value={unit.id}>
                  {unit.displayCode} — {unit.name}
                </option>
              ))}
            </select>
          </div>

          {moveUnit.isError ? (
            <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
              {moveUnit.error.message}
            </p>
          ) : null}

          <div className="flex gap-2">
            <Button type="button" size="sm" disabled={!newParentId || moveUnit.isPending} onClick={onConfirm}>
              {moveUnit.isPending ? t("moving") : t("confirmMove")}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setIsOpen(false)}>
              {t("cancel")}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
