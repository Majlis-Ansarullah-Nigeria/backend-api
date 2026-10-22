/**
 * FR-DSH-004's map colouring and schematic layout. Magnitude, so one hue light → dark (dataviz:
 * sequential = one hue); "no data" is a neutral grey and always labelled, never just a colour.
 */
export const MAP_STEPS = [
  { fill: "var(--color-mint)", text: "var(--color-ink)" },
  { fill: "var(--color-chart-open)", text: "var(--color-ink)" },
  { fill: "var(--color-green-500)", text: "var(--color-surface)" },
  { fill: "var(--color-green-700)", text: "var(--color-surface)" },
] as const;

export const MAP_NO_DATA = { fill: "var(--color-ground)", text: "var(--color-ink-muted)" } as const;

/** Which of the four steps a value falls in. Compliance uses fixed quarters of 0–100%; a figure
 * is placed relative to the other values on screen (min → max), since figures have no natural
 * scale. Null when there's no value. */
export function mapStep(value: number | null, values: (number | null)[], isPercent: boolean): number | null {
  if (value == null) {
    return null;
  }
  if (isPercent) {
    return Math.min(MAP_STEPS.length - 1, Math.max(0, Math.floor(value / 25)));
  }
  const present = values.filter((v): v is number => v != null);
  const min = Math.min(...present);
  const max = Math.max(...present);
  if (max === min) {
    return MAP_STEPS.length - 1;
  }
  return Math.min(MAP_STEPS.length - 1, Math.floor(((value - min) / (max - min)) * MAP_STEPS.length));
}

export interface SchematicItem {
  id: string;
  latitude: number | null;
  longitude: number | null;
}

export interface SchematicCell<T> {
  item: T;
  row: number;
  column: number;
}

/** Lays tiles on a grid `columns` wide, roughly where they sit geographically (north at the top)
 * when at least two have a GPS location; units without one, and any collisions, take the next
 * free cell. Without locations it's a plain grid in the given order. */
export function schematicLayout<T extends SchematicItem>(items: T[], columns = 3): SchematicCell<T>[] {
  const located = items.filter((item) => item.latitude != null && item.longitude != null);
  const rows = Math.max(1, Math.ceil(items.length / columns) + (located.length >= 2 ? 1 : 0));
  const taken = new Set<string>();
  const cells: SchematicCell<T>[] = [];

  const place = (item: T, row: number, column: number) => {
    let index = row * columns + column;
    while (taken.has(String(index))) {
      index += 1;
    }
    taken.add(String(index));
    cells.push({ item, row: Math.floor(index / columns), column: index % columns });
  };

  if (located.length >= 2) {
    const lats = located.map((item) => item.latitude!);
    const lngs = located.map((item) => item.longitude!);
    const [minLat, maxLat, minLng, maxLng] = [Math.min(...lats), Math.max(...lats), Math.min(...lngs), Math.max(...lngs)];
    for (const item of located) {
      const column = maxLng === minLng ? 0 : Math.min(columns - 1, Math.floor(((item.longitude! - minLng) / (maxLng - minLng)) * columns));
      const row = maxLat === minLat ? 0 : Math.min(rows - 1, Math.floor(((maxLat - item.latitude!) / (maxLat - minLat)) * rows));
      place(item, row, column);
    }
  }

  for (const item of items) {
    if (!cells.some((cell) => cell.item === item)) {
      place(item, 0, 0);
    }
  }

  return items.map((item) => cells.find((cell) => cell.item === item)!);
}
