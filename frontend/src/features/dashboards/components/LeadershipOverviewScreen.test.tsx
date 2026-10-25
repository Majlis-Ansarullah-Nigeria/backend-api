import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { LEADERSHIP_PERIOD, LeadershipOverviewScreen } from "./LeadershipOverviewScreen";

const useGetLeadershipOverview = vi.fn();
const useGetFeatureFlags = vi.fn();
vi.mock("@/lib/api/generated/settings/settings", () => ({
  useGetFeatureFlags: (...args: unknown[]) => useGetFeatureFlags(...args),
}));

/** FR-ADM-004 flags as the API returns them; modules 0–3 are Dashboards, CompiledReports,
 * ComparisonAndVersions and WebPush. */
function flags(off: number[] = []) {
  return { data: { canEdit: false, flags: [0, 1, 2, 3].map((module) => ({ module, enabled: !off.includes(module) })) } };
}


vi.mock("@/lib/api/generated/dashboards/dashboards", () => ({
  useGetLeadershipOverview: (...args: unknown[]) => useGetLeadershipOverview(...args),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const OVERVIEW = {
  reportTypeName: "General Monthly Report",
  currentWindowId: "w9",
  currentPeriodLabel: "September",
  previousPeriodLabel: "August",
  period: 0,
  periodLabel: "September",
  received: 140,
  expected: 267,
  closesAt: "2099-10-25T12:00:00Z",
  figures: [
    { fieldKey: "members", label: "Members", type: 2, unitLabel: null, value: 18940, compare: 18550 },
    { fieldKey: "new", label: "New members", type: 2, unitLabel: null, value: 312, compare: null },
    { fieldKey: "funds", label: "Funds", type: 3, unitLabel: "NGN", value: 14_600_000, compare: 14_000_000 },
  ],
  onTimePercent: 88,
  onTimePeriodLabel: "August",
  zoneLevelName: "Muqam",
  zones: [
    { groupUnitId: "z1", name: "Lagos", expected: 100, submitted: 74 },
    { groupUnitId: "z2", name: "North West", expected: 100, submitted: 38 },
  ],
};

beforeEach(() => {
  useGetLeadershipOverview.mockReset();
  useGetFeatureFlags.mockReturnValue(flags());
});

describe("LeadershipOverviewScreen", () => {
  it("shows reports received, headline figures, on-time rate and zones", () => {
    useGetLeadershipOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });

    renderWithIntl(<LeadershipOverviewScreen />);

    expect(screen.getByText("140 of 267")).toBeInTheDocument();
    expect(screen.getByText("Window closes 25 Oct")).toBeInTheDocument();
    expect(screen.getByText("52%")).toBeInTheDocument();
    expect(screen.getByText("18,940")).toBeInTheDocument();
    expect(screen.getByText("+390 vs previous period")).toBeInTheDocument();
    expect(screen.getByText("NGN 14.6M")).toBeInTheDocument();
    expect(screen.getByText("+4.3% vs previous period")).toBeInTheDocument();
    expect(screen.getByText("88%")).toBeInTheDocument();
    const zones = screen.getByRole("heading", { name: "Zones, September received" }).closest("section")!;
    expect(within(zones).getByText("74%")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open compiled report" })).toHaveAttribute("href", "/compiled/w9");
  });

  it("hides the compiled report link when compiled reports are switched off", () => {
    useGetLeadershipOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });
    useGetFeatureFlags.mockReturnValue(flags([1]));

    renderWithIntl(<LeadershipOverviewScreen />);

    expect(screen.getByText("140 of 267")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Open compiled report" })).not.toBeInTheDocument();
  });

  it("switches between the current period, the previous one and the year", async () => {
    useGetLeadershipOverview.mockReturnValue({ isPending: false, isError: false, data: OVERVIEW });

    renderWithIntl(<LeadershipOverviewScreen />);
    expect(useGetLeadershipOverview).toHaveBeenLastCalledWith({ period: LEADERSHIP_PERIOD.Current });

    await userEvent.click(screen.getByRole("button", { name: "Year" }));
    expect(useGetLeadershipOverview).toHaveBeenLastCalledWith({ period: LEADERSHIP_PERIOD.Year });
    await userEvent.click(screen.getByRole("button", { name: "August" }));
    expect(useGetLeadershipOverview).toHaveBeenLastCalledWith({ period: LEADERSHIP_PERIOD.Previous });
  });

  it("shows an error state with retry", async () => {
    const refetch = vi.fn();
    useGetLeadershipOverview.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithIntl(<LeadershipOverviewScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });
});
