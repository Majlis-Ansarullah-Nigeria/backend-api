import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ComplianceDashboardScreen } from "./ComplianceDashboardScreen";

const useGetComplianceDashboard = vi.fn();
const mutate = vi.fn();
const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

vi.mock("@/lib/api/generated/dashboards/dashboards", () => ({
  useGetComplianceDashboard: (...args: unknown[]) => useGetComplianceDashboard(...args),
  useSendComplianceReminders: () => ({ mutate, isPending: false }),
}));

// Recharts needs real layout to draw; the chart's own rendering isn't what's under test here.
vi.mock("./OnTimeLateChart", () => ({
  OnTimeLateChart: ({ labels }: { labels: { ariaLabel: string } }) => <div role="img" aria-label={labels.ariaLabel} />,
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const period = (label: string, isClosed: boolean, onTime: number, late: number, missed: number, drafting = 0, notStarted = 0) => ({
  windowId: `w-${label}`,
  periodLabel: label,
  dueAt: "2026-10-05T00:00:00Z",
  isClosed,
  expected: 100,
  onTime,
  late,
  missed,
  drafting,
  notStarted,
});

const DASHBOARD = {
  reportTypes: [
    { id: "t1", name: "General Monthly Report" },
    { id: "t2", name: "Quarterly Report" },
  ],
  reportTypeId: "t1",
  reportTypeName: "General Monthly Report",
  levels: [
    { code: 3, name: "Muqam", pluralName: "Muqamaat" },
    { code: 2, name: "Dila", pluralName: "Dilaat" },
  ],
  level: 3,
  periods: [period("Jul", true, 86, 6, 8), period("Aug", true, 88, 6, 6), period("Sep", false, 50, 10, 0, 20, 20)],
  groups: [
    { groupUnitId: "z1", name: "Lagos Zone", expected: 66, submitted: 49, drafting: 7, notStarted: 10, remindable: 17 },
    { groupUnitId: "z2", name: "North West Zone", expected: 20, submitted: 0, drafting: 0, notStarted: 0, remindable: 0 },
  ],
  canRemind: true,
};

beforeEach(() => {
  useGetComplianceDashboard.mockReset();
  mutate.mockReset();
  replace.mockReset();
});

describe("ComplianceDashboardScreen", () => {
  it("shows the KPI tiles from the last closed period", () => {
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<ComplianceDashboardScreen initial={{}} />);

    expect(screen.getByText("General Monthly Report · last 3 periods")).toBeInTheDocument();
    expect(screen.getByText("88%")).toBeInTheDocument();
    expect(screen.getByText("Aug · up 2 points")).toBeInTheDocument();
    expect(screen.getByText("87%")).toBeInTheDocument();
    expect(screen.getByText("Late, Aug")).toBeInTheDocument();
    expect(screen.getAllByText("6 Muqamaat")).toHaveLength(2);
    expect(screen.getByRole("img", { name: /stacked bar chart/i })).toBeInTheDocument();
  });

  it("lists the latest period by zone and reminds a zone's unsubmitted units", async () => {
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<ComplianceDashboardScreen initial={{}} />);

    expect(screen.getByRole("heading", { name: "Sep so far, by zone" })).toBeInTheDocument();
    const lagos = screen.getByRole("row", { name: /lagos zone/i });
    expect(within(lagos).getByText("74%")).toBeInTheDocument();
    expect(within(lagos).getByText("49 / 66")).toBeInTheDocument();

    await userEvent.click(within(lagos).getByRole("button", { name: /remind lagos zone: 17 units/i }));
    expect(mutate).toHaveBeenCalledWith(
      { data: { windowId: "w-Sep", level: 3, groupUnitId: "z1" } },
      expect.anything(),
    );

    const options = mutate.mock.calls[0][1] as { onSuccess: (result: unknown) => void };
    options.onSuccess({ unitsReminded: 16, unitsWithoutHead: 1 });
    expect(await screen.findByRole("status")).toHaveTextContent("Lagos Zone: reminded 16 units. 1 unit has no head to remind.");

    const northWest = screen.getByRole("row", { name: /north west zone/i });
    expect(within(northWest).getByRole("button", { name: /remind/i })).toBeDisabled();
  });

  it("hides Remind for read-only viewers", () => {
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: false, data: { ...DASHBOARD, canRemind: false } });

    renderWithIntl(<ComplianceDashboardScreen initial={{}} />);

    expect(screen.queryByRole("button", { name: /remind/i })).not.toBeInTheDocument();
  });

  it("switches level and report type through the URL", async () => {
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: false, data: DASHBOARD });

    renderWithIntl(<ComplianceDashboardScreen initial={{}} />);
    await userEvent.click(screen.getByRole("button", { name: "Dila" }));
    expect(replace).toHaveBeenLastCalledWith("/secretariat/dashboards/compliance?level=2");

    await userEvent.selectOptions(screen.getByLabelText("Report type"), "t2");
    expect(replace).toHaveBeenLastCalledWith("/secretariat/dashboards/compliance?reportTypeId=t2");
  });

  it("shows empty and error states", async () => {
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: false, data: { ...DASHBOARD, periods: [], groups: [] } });
    const { unmount } = renderWithIntl(<ComplianceDashboardScreen initial={{}} />);
    expect(screen.getByText("No periods yet")).toBeInTheDocument();
    unmount();

    const refetch = vi.fn();
    useGetComplianceDashboard.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });
    renderWithIntl(<ComplianceDashboardScreen initial={{}} />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));
    expect(refetch).toHaveBeenCalled();
  });
});
