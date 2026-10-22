"use client";

import { Map as MapIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import dynamic from "next/dynamic";
import { useState } from "react";
import { ApiError } from "@/lib/api/client";
import type { MapView } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMapView } from "@/lib/api/generated/dashboards/dashboards";
import { formatIndicator } from "@/features/dashboards/utils/indicators";
import { MAP_NO_DATA, MAP_STEPS, mapStep, schematicLayout } from "@/features/dashboards/utils/map";
import { cn } from "@/lib/utils";

const LeafletMap = dynamic(() => import("./LeafletMap"), { ssr: false });

// Matches backend/src/HRS.Application/Features/Analytics/GetMapViewQuery.cs's MapMeasure.
export const MAP_MEASURE = { Indicator: 0, Compliance: 1 } as const;

const toNumber = (value: number | string | null | undefined): number | null => (value == null ? null : Number(value));

/** FR-DSH-004: the dashboard's "Map view" card — a schematic of the node's children by default,
 * or a real map of GPS-pinned units; coloured by the selected figure or by compliance. */
export function MapCard({
  reportTypeId,
  fieldKey,
  unitId,
  onOpenUnit,
}: {
  reportTypeId?: string;
  fieldKey?: string;
  unitId?: string;
  onOpenUnit: (unitId: string) => void;
}) {
  const t = useTranslations("MapView");
  const [measure, setMeasure] = useState<number>(MAP_MEASURE.Indicator);
  const [view, setView] = useState<"schematic" | "map">("schematic");
  const query = useGetMapView<MapView, ApiError>({ reportTypeId, fieldKey, unitId, measure });

  const data = query.data;
  const isPercent = measure === MAP_MEASURE.Compliance;
  const tiles = (data?.tiles ?? []).map((tile) => ({
    ...tile,
    id: tile.unitId,
    latitude: toNumber(tile.latitude),
    longitude: toNumber(tile.longitude),
    value: toNumber(tile.value),
  }));
  const tileValues = tiles.map((tile) => tile.value);
  const label = (value: number | null) =>
    value == null
      ? t("noData")
      : isPercent
        ? t("compliancePercent", { value: Math.round(value) })
        : formatIndicator(value, Number(data?.field?.type ?? 2), data?.field?.unitLabel);
  const pins = (data?.pins ?? []).map((pin) => ({ ...pin, value: toNumber(pin.value) }));
  const pinValues = pins.map((pin) => pin.value);
  const cells = schematicLayout(tiles);
  const rows = Math.max(1, ...cells.map((cell) => cell.row + 1));

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:p-6" aria-labelledby="map-card-title">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="map-card-title" className="flex items-center gap-2 text-lg font-bold text-ink">
          <MapIcon className="size-5 text-ink-muted" aria-hidden="true" />
          {t("title")}
        </h2>
        <div className="flex flex-wrap gap-2">
          <Toggle
            label={t("measureLabel")}
            options={[
              { value: MAP_MEASURE.Indicator, label: data?.field?.label ?? t("measureFigure") },
              { value: MAP_MEASURE.Compliance, label: t("measureCompliance") },
            ]}
            selected={measure}
            onSelect={setMeasure}
          />
          <Toggle
            label={t("viewLabel")}
            options={[
              { value: "schematic" as const, label: t("viewSchematic") },
              { value: "map" as const, label: t("viewMap") },
            ]}
            selected={view}
            onSelect={setView}
          />
        </div>
      </div>

      {query.isPending ? (
        <p className="text-sm text-ink-muted">{t("loading")}</p>
      ) : query.isError ? (
        <p className="text-sm text-danger">{t("loadError")}</p>
      ) : view === "schematic" ? (
        tiles.length === 0 ? (
          <p className="text-sm text-ink-muted">{t("noTiles")}</p>
        ) : (
          <ul className="grid grid-cols-3 gap-2" style={{ gridTemplateRows: `repeat(${rows}, minmax(3.5rem, auto))` }}>
            {cells.map(({ item, row, column }) => {
              const step = mapStep(item.value, tileValues, isPercent);
              const colour = step == null ? MAP_NO_DATA : MAP_STEPS[step];
              const name = `${item.name}: ${label(item.value)}`;
              return (
                <li key={item.id} style={{ gridRow: row + 1, gridColumn: column + 1 }}>
                  <button
                    type="button"
                    onClick={() => onOpenUnit(item.unitId)}
                    disabled={!item.hasChildren}
                    aria-label={name}
                    title={name}
                    className="flex h-full min-h-14 w-full items-center justify-center rounded-md px-2 text-sm font-bold disabled:cursor-default focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-lime"
                    style={{ backgroundColor: colour.fill, color: colour.text }}
                  >
                    {item.code || item.name}
                  </button>
                </li>
              );
            })}
          </ul>
        )
      ) : pins.length === 0 ? (
        <p className="text-sm text-ink-muted">{t("noPins")}</p>
      ) : (
        <LeafletMap
          ariaLabel={t("mapAriaLabel")}
          pins={pins.map((pin) => ({
            id: pin.unitId,
            name: pin.name,
            latitude: Number(pin.latitude),
            longitude: Number(pin.longitude),
            step: mapStep(pin.value, pinValues, isPercent),
            valueLabel: label(pin.value),
          }))}
        />
      )}

      <div className="flex flex-wrap items-center gap-3 text-xs text-ink-muted">
        <span>
          {isPercent ? t("legendCompliance") : t("legendFigure")}
          {data?.periodLabel ? ` · ${data.periodLabel}` : null}
        </span>
        <span className="flex items-center gap-1" aria-hidden="true">
          {t("legendLow")}
          {MAP_STEPS.map((step, index) => (
            <span key={index} className="size-3 rounded-sm" style={{ backgroundColor: step.fill }} />
          ))}
          {t("legendHigh")}
        </span>
        <span className="flex items-center gap-1">
          <span className="size-3 rounded-sm border border-line-strong" style={{ backgroundColor: MAP_NO_DATA.fill }} aria-hidden="true" />
          {t("noData")}
        </span>
      </div>
      <p className="text-xs text-ink-muted">{view === "schematic" ? t("schematicNote") : t("mapNote")}</p>
    </section>
  );
}

function Toggle<T extends string | number>({
  label,
  options,
  selected,
  onSelect,
}: {
  label: string;
  options: { value: T; label: string }[];
  selected: T;
  onSelect: (value: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="flex rounded-md bg-mint/60 p-1">
      {options.map((option) => (
        <button
          key={String(option.value)}
          type="button"
          aria-pressed={selected === option.value}
          onClick={() => onSelect(option.value)}
          className={cn(
            "h-9 rounded-sm px-3 text-sm font-semibold",
            selected === option.value ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink",
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
