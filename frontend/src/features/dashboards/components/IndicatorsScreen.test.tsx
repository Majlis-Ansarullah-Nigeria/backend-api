import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { IndicatorsScreen } from "./IndicatorsScreen";

const useGetIndicatorDashboard = vi.fn();
const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

vi.mock("@/lib/api/generated/dashboards/dashboards", () => ({
  useGetIndicatorDashboard: (...args: unknown[]) => useGetIndicatorDashboard(...args),
}));

// The map card has its own tests (MapCard.test.tsx).
vi.mock("./MapCard", () => ({
  MapCard: () => <div>Map view card</div>,
}));

// Recharts needs real layout to draw; the chart's own rendering isn't what's under test here.
vi.mock("./IndicatorChart", () => ({
  IndicatorChart: ({ labels, mode }: { labels: { ariaLabel: string; target: string }; mode: string }) => (
    <div role="img" aria-label={labels.ariaLabel} data-mode={mode}>
      {labels.target}
    </div>
  ),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const DASHBOARD = {
  reportTypes: [{ id: "t1", name: "General Monthly Report" }],
  reportTypeId: "t1",
  reportTypeName: "General Monthly Report",
  fields: [
    { fieldKey: "members", label: "Total members", type: 2, unitLabel: null, aggregation: 1 },
    { fieldKey: "funds", label: "Funds collected", type: 3, unitLabel: "NGN", aggregation: 1 },
    { fieldKey: "attendance", label: "Attendance rate", type: 4, unitLabel: null, aggregation: 2 },
  ],
  fieldKey: "members",
  node: { unitId: "z1", name: "South West Zone", level: 1, levelName: "Zone" },
  breadcrumb: [
    { unitId: "nat", name: "National", level: 0, levelName: "National" },
    { unitId: "z1", name: "South West Zone", level: 1, levelName: "Zone" },
  ],
  childLevelPluralName: "Dilaat",
  periods: [
    { windowId: "w7", periodLabel: "Jul", opensAt: "2026-07-01T00:00:00Z" },
    { windowId: "w8", periodLabel: "Aug", opensAt: "2026-08-01T00:00:00Z" },
  ],
  series: [
    { fieldKey: "members", values: [3120, 3702], target: 3800 },
    { fieldKey: "funds", values: [3_630_000, 3_850_000], target: null },
    { fieldKey: "attendance", values: [63, 61], target: null },
  ],
  children: [
    { unitId: "d1", name: "Ibadan Dila", level: 2, hasChildren: true, first: 1043, latest: 1064, target: 1156 },
    { unitId: "d2", name: "Osogbo Dila", level: 2, hasChildren: true, first: 582, latest: 588, target: 828 },
  ],
};

beforeEach(() => {
  useGetIndicatorDashboard.mockReset();
  replace.mockReset();
});

describe("IndicatorsScreen", () => {
  it("shows the breadcrumb, KPI tiles with targets and changes, and the target line", () => {
    useGetIndicatorDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<IndicatorsScreen basePath="/secretariat/dashboards/indicators" initial={{}} />);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Indicators · South West Zone");
    expect(screen.getByText("Choose a Dilaat")).toBeInTheDocument();
    expect(screen.getByText("From acknowledged General Monthly Report reports")).toBeInTheDocument();
    expect(screen.getByText("Target 3,800", { selector: "p" })).toBeInTheDocument();
    expect(screen.getByText("NGN 3.85M")).toBeInTheDocument();
    expect(screen.getByText("+6.1% vs Jul")).toBeInTheDocument();
    expect(screen.getByText("-2 points vs Jul")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /chart of total members/i })).toHaveTextContent("Target 3,800");
    expect(screen.getByRole("link", { name: "Indicators" })).toHaveAttribute("aria-current", "page");
  });

  it("drills down through the children table and back up through the breadcrumb", async () => {
    useGetIndicatorDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<IndicatorsScreen basePath="/dashboards" initial={{}} />);

    expect(screen.queryByRole("link", { name: "Indicators" })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Drill down: Dilaat in South West Zone" })).toBeInTheDocument();
    const ibadan = screen.getByRole("row", { name: /ibadan dila/i });
    expect(within(ibadan).getByText("1,064")).toBeInTheDocument();
    expect(within(ibadan).getByText("+21")).toBeInTheDocument();
    expect(within(ibadan).getByText("92%")).toBeInTheDocument();
    expect(within(ibadan).getByRole("link", { name: "Ibadan Dila" })).toHaveAttribute("href", "/dashboards?unitId=d1");

    await userEvent.click(screen.getByRole("button", { name: "National" }));
    expect(replace).toHaveBeenCalledWith("/dashboards?unitId=nat");
  });

  it("switches the figure, the range and the chart style", async () => {
    useGetIndicatorDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<IndicatorsScreen basePath="/dashboards" initial={{}} />);

    await userEvent.selectOptions(screen.getByLabelText("Figure"), "funds");
    expect(replace).toHaveBeenLastCalledWith("/dashboards?fieldKey=funds");
    await userEvent.selectOptions(screen.getByLabelText("Range"), "24");
    expect(replace).toHaveBeenLastCalledWith("/dashboards?fieldKey=funds&periods=24");
    await userEvent.click(screen.getByRole("button", { name: "Bars" }));
    expect(screen.getByRole("img", { name: /chart of/i })).toHaveAttribute("data-mode", "bars");
  });

  it("explains a forbidden scope and offers retry on errors", async () => {
    const refetch = vi.fn();
    useGetIndicatorDashboard.mockReturnValue({ isPending: false, isError: true, error: { status: 403 }, data: undefined, refetch });

    renderWithIntl(<IndicatorsScreen basePath="/dashboards" initial={{ unitId: "x" }} />);

    expect(screen.getByText(/outside your dashboard/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state without figures", () => {
    useGetIndicatorDashboard.mockReturnValue({ isPending: false, isError: false, data: { ...DASHBOARD, fields: [], series: [], children: [] } });

    renderWithIntl(<IndicatorsScreen basePath="/dashboards" initial={{}} />);

    expect(screen.getByText("No figures yet")).toBeInTheDocument();
  });
});
