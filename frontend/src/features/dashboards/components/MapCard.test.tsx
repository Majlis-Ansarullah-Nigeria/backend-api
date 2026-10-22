import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { MAP_MEASURE, MapCard } from "./MapCard";

const useGetMapView = vi.fn();

vi.mock("@/lib/api/generated/dashboards/dashboards", () => ({
  useGetMapView: (...args: unknown[]) => useGetMapView(...args),
}));

// Leaflet needs a real browser layout; the map itself isn't what's under test.
vi.mock("next/dynamic", () => ({
  default: () => (props: { pins: { name: string; valueLabel: string }[] }) => (
    <ul aria-label="Leaflet map">
      {props.pins.map((pin) => (
        <li key={pin.name}>{`${pin.name} · ${pin.valueLabel}`}</li>
      ))}
    </ul>
  ),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const tile = (unitId: string, code: string, value: number | null, hasChildren = true, latitude: number | null = null, longitude: number | null = null) => ({
  unitId,
  name: `${code} Zone`,
  code,
  level: 1,
  latitude,
  longitude,
  value,
  hasChildren,
});

const VIEW = {
  measure: 0,
  field: { fieldKey: "members", label: "Total members", type: 2, unitLabel: null, aggregation: 1 },
  periodLabel: "Sep 2026",
  tiles: [tile("z1", "SW", 1064), tile("z2", "NW", 310), tile("z3", "NE", null, false)],
  pins: [tile("d1", "IB", 1064, true, 7.38, 3.94)],
};

beforeEach(() => {
  useGetMapView.mockReset();
  useGetMapView.mockReturnValue({ isPending: false, isError: false, data: VIEW });
});

describe("MapCard", () => {
  it("shows a schematic tile per child with its value and drills into it", async () => {
    const onOpenUnit = vi.fn();
    renderWithIntl(<MapCard reportTypeId="t1" unitId="nat" onOpenUnit={onOpenUnit} />);

    expect(screen.getByRole("button", { name: "SW Zone: 1,064" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "NE Zone: No data" })).toBeDisabled();
    expect(screen.getByText("Latest figures · Sep 2026")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "SW Zone: 1,064" }));
    expect(onOpenUnit).toHaveBeenCalledWith("z1");
  });

  it("switches to colouring by compliance", async () => {
    renderWithIntl(<MapCard reportTypeId="t1" unitId="nat" onOpenUnit={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: "Compliance" }));

    expect(useGetMapView).toHaveBeenLastCalledWith(expect.objectContaining({ measure: MAP_MEASURE.Compliance }));
  });

  it("shows GPS-pinned units on the real map, or explains when there are none", async () => {
    const { unmount } = renderWithIntl(<MapCard reportTypeId="t1" unitId="nat" onOpenUnit={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: "Map" }));
    expect(screen.getByText("IB Zone · 1,064")).toBeInTheDocument();
    unmount();

    useGetMapView.mockReturnValue({ isPending: false, isError: false, data: { ...VIEW, pins: [] } });
    renderWithIntl(<MapCard reportTypeId="t1" unitId="nat" onOpenUnit={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: "Map" }));
    expect(screen.getByText("No units in this view have a GPS location yet.")).toBeInTheDocument();
  });
});
