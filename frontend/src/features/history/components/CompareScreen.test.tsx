import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { CompareScreen } from "./CompareScreen";

const useGetComparisonOptions = vi.fn();
const useGetPeriodComparison = vi.fn();
const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

vi.mock("@/lib/api/generated/history/history", () => ({
  useGetComparisonOptions: (...args: unknown[]) => useGetComparisonOptions(...args),
  useGetPeriodComparison: (...args: unknown[]) => useGetPeriodComparison(...args),
}));

// Recharts needs real layout to draw; the chart's own rendering isn't what's under test here.
vi.mock("./TrendChart", () => ({
  TrendChart: ({ ariaLabel }: { ariaLabel: string }) => <div role="img" aria-label={ariaLabel} />,
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

const OPTIONS = [{ unitId: "u1", unitName: "Agodi Muqam", reportTypes: [{ id: "t1", name: "General Monthly Report" }] }];

const COMPARISON = {
  unitId: "u1",
  unitName: "Agodi Muqam",
  reportTypeId: "t1",
  reportTypeName: "General Monthly Report",
  periods: [
    { windowId: "w7", periodLabel: "July 2026", opensAt: "2026-07-01T00:00:00Z", isDraft: false },
    { windowId: "w8", periodLabel: "August 2026", opensAt: "2026-08-01T00:00:00Z", isDraft: false },
    { windowId: "w9", periodLabel: "September 2026", opensAt: "2026-09-01T00:00:00Z", isDraft: true },
  ],
  periodAWindowId: "w8",
  periodBWindowId: "w9",
  figures: [
    { fieldKey: "members", label: "Total members", type: 2, unitLabel: null, periodA: 142, periodB: 148 },
    { fieldKey: "funds", label: "Funds collected (NGN)", type: 3, unitLabel: null, periodA: 398000, periodB: 412500 },
    { fieldKey: "families", label: "Families supported", type: 2, unitLabel: null, periodA: 3, periodB: 1 },
  ],
  changedTextSections: ["Highlights", "Plans"],
  series: [
    { windowId: "w7", periodLabel: "July 2026", values: { members: 140, funds: 390000, families: 2 } },
    { windowId: "w8", periodLabel: "August 2026", values: { members: 142, funds: 398000, families: 3 } },
    { windowId: "w9", periodLabel: "September 2026", values: { members: 148, funds: 412500, families: 1 } },
  ],
};

beforeEach(() => {
  useGetComparisonOptions.mockReset();
  useGetPeriodComparison.mockReset();
  replace.mockReset();
  useGetComparisonOptions.mockReturnValue({ isPending: false, isError: false, data: OPTIONS });
});

describe("CompareScreen", () => {
  it("defaults to the first unit and report type and shows both periods side by side", () => {
    useGetPeriodComparison.mockReturnValue({ isPending: false, isError: false, data: COMPARISON });

    renderWithIntl(<CompareScreen initial={{}} />);

    expect(useGetPeriodComparison).toHaveBeenCalledWith(
      expect.objectContaining({ unitId: "u1", reportTypeId: "t1" }),
      expect.anything(),
    );
    expect(screen.getByText("Agodi Muqam · General Monthly Report")).toBeInTheDocument();
    expect(screen.getByLabelText("Period A")).toHaveValue("w8");
    expect(screen.getByLabelText("Period B")).toHaveValue("w9");
    expect(within(screen.getByLabelText("Period B")).getByRole("option", { name: "September 2026 (draft)" })).toBeInTheDocument();

    const table = screen.getByRole("table");
    expect(within(table).getByRole("columnheader", { name: "August 2026" })).toBeInTheDocument();
    const membersRow = within(table).getByRole("row", { name: /total members/i });
    expect(within(membersRow).getByText("+6")).toBeInTheDocument();
    const fundsRow = within(table).getByRole("row", { name: /funds collected/i });
    expect(within(fundsRow).getByText("+14,500")).toBeInTheDocument();
    expect(within(fundsRow).getByText("(+3.6%)")).toBeInTheDocument();
    expect(within(within(table).getByRole("row", { name: /families supported/i })).getByText("-2")).toBeInTheDocument();
  });

  it("summarises what changed", () => {
    useGetPeriodComparison.mockReturnValue({ isPending: false, isError: false, data: COMPARISON });

    renderWithIntl(<CompareScreen initial={{}} />);

    expect(screen.getByText("Total members grew by 6, the largest rise in the last 3 periods.")).toBeInTheDocument();
    expect(screen.getByText("Families supported fell from 3 to 1.")).toBeInTheDocument();
    expect(screen.getByText("Text changed in 2 sections.")).toBeInTheDocument();
  });

  it("charts the chosen figure and updates the URL when a period changes", async () => {
    useGetPeriodComparison.mockReturnValue({ isPending: false, isError: false, data: COMPARISON });

    renderWithIntl(<CompareScreen initial={{}} />);
    expect(screen.getByRole("img", { name: /total members by period/i })).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText("Figure to chart"), "funds");
    expect(screen.getByRole("img", { name: /funds collected \(NGN\) by period/i })).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText("Period A"), "w7");
    expect(replace).toHaveBeenCalledWith("/history/compare?unitId=u1&reportTypeId=t1&periodA=w7&periodB=w9");
  });

  it("shows an empty state when there's only one period", () => {
    useGetPeriodComparison.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...COMPARISON, periods: COMPARISON.periods.slice(0, 1) },
    });

    renderWithIntl(<CompareScreen initial={{}} />);

    expect(screen.getByText(/nothing to compare yet/i)).toBeInTheDocument();
  });

  it("shows an error state with retry", async () => {
    const refetch = vi.fn();
    useGetPeriodComparison.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithIntl(<CompareScreen initial={{}} />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });
});
