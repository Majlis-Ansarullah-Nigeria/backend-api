"use client";

import "leaflet/dist/leaflet.css";
import { CircleMarker, MapContainer, TileLayer, Tooltip } from "react-leaflet";
import { MAP_NO_DATA, MAP_STEPS } from "@/features/dashboards/utils/map";

export interface LeafletPin {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  step: number | null;
  valueLabel: string;
}

/** FR-DSH-004's real map: GPS-pinned units on OpenStreetMap tiles, coloured on the same scale as
 * the schematic. Loaded client-side only (Leaflet needs the DOM). */
/** Leaflet writes colours into SVG attributes, which can't read CSS variables — so resolve the
 * design tokens to their actual values first. */
function resolveToken(value: string): string {
  const match = /^var\((--[\w-]+)\)$/.exec(value);
  return match ? getComputedStyle(document.documentElement).getPropertyValue(match[1]).trim() || value : value;
}

export default function LeafletMap({ pins, ariaLabel }: { pins: LeafletPin[]; ariaLabel: string }) {
  const bounds = pins.map((pin) => [pin.latitude, pin.longitude] as [number, number]);

  return (
    <div role="region" aria-label={ariaLabel} className="h-72 overflow-hidden rounded-md">
      <MapContainer
        bounds={bounds.length > 1 ? bounds : undefined}
        center={bounds.length === 1 ? bounds[0] : undefined}
        zoom={bounds.length === 1 ? 11 : undefined}
        boundsOptions={{ padding: [24, 24] }}
        scrollWheelZoom={false}
        className="h-full w-full"
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        {pins.map((pin) => {
          const colour = resolveToken(pin.step == null ? MAP_NO_DATA.fill : MAP_STEPS[pin.step].fill);
          return (
            <CircleMarker
              key={pin.id}
              center={[pin.latitude, pin.longitude]}
              radius={9}
              pathOptions={{ color: resolveToken("var(--color-surface)"), weight: 2, fillColor: colour, fillOpacity: 1 }}
            >
              <Tooltip>
                {pin.name} · {pin.valueLabel}
              </Tooltip>
            </CircleMarker>
          );
        })}
      </MapContainer>
    </div>
  );
}
