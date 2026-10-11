import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ComplianceGridScreen } from "./ComplianceGridScreen";

const useGetComplianceGrid = vi.fn();
const sendNudge = vi.fn();

vi.mock("@/lib/api/generated/observer/observer", () => ({
  useGetComplianceGrid: (...args: unknown[]) => useGetComplianceGrid(...args),
  sendNudge: (...args: unknown[]) => sendNudge(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const EMPTY_GRID = {
  parentUnitId: null,
  parentUnitName: null,
  childLevelName: null,
  childLevelPluralName: null,
  availableParentUnits: [],
  availableReportTypes: [],
  selectedReportTypeId: null,
  periods: [],
  rows: [],
};

const FULL_GRID = {
  parentUnitId: "dila-1",
  parentUnitName: "Ibadan Dila",
  childLevelName: "Muqam",
  childLevelPluralName: "Muqamaat",
  availableParentUnits: [{ id: "dila-1", name: "Ibadan Dila" }],
  availableReportTypes: [{ id: "rt-1", name: "General Monthly" }],
  selectedReportTypeId: "rt-1",
  periods: [{ windowId: "w-1", periodLabel: "Sep 2026", dueAt: "2026-09-10T00:00:00Z" }],
  rows: [
    { unitId: "m-1", unitName: "Agodi Muqam", headName: "A. Adeyemi", hasVacantHead: false, cells: [{ windowId: "w-1", status: 4, reportInstanceId: "ri-1" }] },
    { unitId: "m-2", unitName: "Mokola Muqam", headName: null, hasVacantHead: true, cells: [{ windowId: "w-1", status: 1, reportInstanceId: null }] },
  ],
};

const OVERDUE_GRID = {
  ...FULL_GRID,
  rows: [
    ...FULL_GRID.rows,
    { unitId: "m-3", unitName: "Challenge Muqam", headName: "F. Lawal", hasVacantHead: false, cells: [{ windowId: "w-1", status: 3, reportInstanceId: null }] },
  ],
};

beforeEach(() => {
  useGetComplianceGrid.mockReset();
  sendNudge.mockReset();
});

describe("ComplianceGridScreen", () => {
  it("shows a loading message while the grid loads", () => {
    useGetComplianceGrid.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<ComplianceGridScreen />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state when the caller observes no unit", () => {
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: false, data: EMPTY_GRID });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByText(/no units to observe/i)).toBeInTheDocument();
  });

  it("renders the grid with a vacant-head banner and both unit rows", () => {
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: false, data: FULL_GRID });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByText("My Muqamaat")).toBeInTheDocument();
    expect(screen.getByText("Agodi Muqam")).toBeInTheDocument();
    expect(screen.getAllByText("Mokola Muqam").length).toBeGreaterThan(0);
    expect(screen.getByText(/no head assigned yet: mokola muqam/i)).toBeInTheDocument();
  });

  it("shows a 'no report types' empty state when none are available", () => {
    useGetComplianceGrid.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...FULL_GRID, availableReportTypes: [], selectedReportTypeId: null, periods: [] },
    });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByText(/nothing to show yet/i)).toBeInTheDocument();
  });

  it("shows a 'no periods' empty state when the report type has no windows yet", () => {
    useGetComplianceGrid.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...FULL_GRID, periods: [], rows: [{ ...FULL_GRID.rows[0], cells: [] }] },
    });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByText(/no periods yet/i)).toBeInTheDocument();
  });

  it("switches the selected report type on tab click", async () => {
    useGetComplianceGrid.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        ...FULL_GRID,
        availableReportTypes: [{ id: "rt-1", name: "General Monthly" }, { id: "rt-2", name: "Youth Camp" }],
      },
    });
    const user = userEvent.setup();

    renderWithProviders(<ComplianceGridScreen />);
    await user.click(screen.getByRole("tab", { name: "Youth Camp" }));

    const lastCallParams = useGetComplianceGrid.mock.calls.at(-1)?.[0];
    expect(lastCallParams).toMatchObject({ reportTypeId: "rt-2" });
  });

  it("shows a Nudge button only for the row whose latest status is Overdue", () => {
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: false, data: OVERDUE_GRID });

    renderWithProviders(<ComplianceGridScreen />);

    expect(screen.getByRole("button", { name: /^nudge$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /nudge all overdue \(1\)/i })).toBeInTheDocument();
  });

  it("sends a nudge for the right unit and window on row-button click", async () => {
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: false, data: OVERDUE_GRID });
    sendNudge.mockResolvedValue(undefined);
    const user = userEvent.setup();

    renderWithProviders(<ComplianceGridScreen />);
    await user.click(screen.getByRole("button", { name: /^nudge$/i }));

    expect(sendNudge).toHaveBeenCalledWith("m-3", { windowId: "w-1" });
    expect(await screen.findByText(/nudged/i)).toBeInTheDocument();
  });

  it("'Nudge all overdue' sends a nudge for every currently-overdue row", async () => {
    useGetComplianceGrid.mockReturnValue({ isPending: false, isError: false, data: OVERDUE_GRID });
    sendNudge.mockResolvedValue(undefined);
    const user = userEvent.setup();

    renderWithProviders(<ComplianceGridScreen />);
    await user.click(screen.getByRole("button", { name: /nudge all overdue/i }));

    expect(sendNudge).toHaveBeenCalledTimes(1);
    expect(sendNudge).toHaveBeenCalledWith("m-3", { windowId: "w-1" });
  });
});
