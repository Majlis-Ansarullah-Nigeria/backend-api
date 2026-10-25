import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { HistoryScreen } from "./HistoryScreen";

const useGetHistory = vi.fn();
const useGetHistoryFilters = vi.fn();
const createBulkExport = vi.fn();
const getHistoryReportPdf = vi.fn();
const getHistoryReportExcel = vi.fn();
const push = vi.fn();
const useGetFeatureFlags = vi.fn();

vi.mock("@/lib/api/generated/settings/settings", () => ({
  useGetFeatureFlags: (...args: unknown[]) => useGetFeatureFlags(...args),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api/generated/history/history", () => ({
  useGetHistory: (...args: unknown[]) => useGetHistory(...args),
  useGetHistoryFilters: (...args: unknown[]) => useGetHistoryFilters(...args),
  createBulkExport: (...args: unknown[]) => createBulkExport(...args),
  getHistoryReportPdf: (...args: unknown[]) => getHistoryReportPdf(...args),
  getHistoryReportExcel: (...args: unknown[]) => getHistoryReportExcel(...args),
}));

vi.mock("@/features/organization/utils/bulk-import", () => ({
  downloadBlob: vi.fn(),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const ROWS = [
  {
    instanceId: "r1",
    windowId: "w1",
    reportTypeId: "t1",
    reportTypeName: "General Monthly Report",
    unitId: "u1",
    unitName: "Agodi Muqam",
    periodLabel: "Oct 2026",
    submittedAt: "2026-10-01T00:00:00Z",
    status: 4,
    submittedByLabel: "You",
    isObserved: false,
  },
  {
    instanceId: "r2",
    windowId: "w1",
    reportTypeId: "t1",
    reportTypeName: "General Monthly Report",
    unitId: "u2",
    unitName: "Ketu Muqam",
    periodLabel: "Oct 2026",
    submittedAt: "2026-10-02T00:00:00Z",
    status: 8,
    submittedByLabel: "Amina Bello",
    isObserved: true,
  },
];

beforeEach(() => {
  useGetHistory.mockReset();
  useGetHistoryFilters.mockReset();
  createBulkExport.mockReset();
  getHistoryReportPdf.mockReset();
  getHistoryReportExcel.mockReset();
  push.mockReset();
  useGetFeatureFlags.mockReturnValue({ data: { canEdit: false, flags: [] } });
  useGetHistoryFilters.mockReturnValue({
    data: { reportTypes: [{ id: "t1", name: "General Monthly Report" }], periods: [{ windowId: "w1", periodLabel: "Oct 2026", opensAt: "2026-10-01T00:00:00Z" }] },
  });
});

describe("HistoryScreen", () => {
  it("links to Compare periods and to each row's version history", () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    expect(screen.getByRole("link", { name: /compare periods/i })).toHaveAttribute("href", "/history/compare");
    expect(screen.getAllByRole("link", { name: /version history of general monthly report/i })[0]).toHaveAttribute(
      "href",
      "/history/r1/versions",
    );
  });

  it("hides Compare periods and version history when that module is switched off", () => {
    useGetFeatureFlags.mockReturnValue({ data: { canEdit: false, flags: [{ module: 2, enabled: false }] } });
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    expect(screen.queryByRole("link", { name: /compare periods/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /version history of/i })).not.toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: /view/i }).length).toBeGreaterThan(0);
  });

  it("shows a loading message while history loads", () => {
    useGetHistory.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<HistoryScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetHistory.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<HistoryScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state when there are no past reports", () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: [], totalCount: 0, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    expect(screen.getByText(/nothing here yet/i)).toBeInTheDocument();
  });

  it("renders each row's report, unit, status and submitter", () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    const dataRows = screen.getAllByRole("row").slice(1);
    const firstRow = within(dataRows[0]!);
    const secondRow = within(dataRows[1]!);
    expect(firstRow.getByText("Agodi Muqam")).toBeInTheDocument();
    expect(firstRow.getByText("You")).toBeInTheDocument();
    expect(firstRow.getByText("Submitted")).toBeInTheDocument();
    expect(secondRow.getByText("Ketu Muqam")).toBeInTheDocument();
    expect(secondRow.getByText("Amina Bello")).toBeInTheDocument();
    expect(secondRow.getByText("Acknowledged")).toBeInTheDocument();
  });

  it("links an own-unit row to the editable report and an observed row to the read-only child view", () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    const viewLinks = screen.getAllByRole("link", { name: /^view$/i });
    expect(viewLinks[0]).toHaveAttribute("href", "/reports/r1/edit?unit=u1");
    expect(viewLinks[1]).toHaveAttribute("href", "/units/u2/reports/r2");
  });

  it("shows the showing-count and disables Previous on the first page", () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 25, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);

    expect(screen.getByText("Showing 2 of 25")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole("button", { name: /^next$/i })).toBeEnabled();
  });

  it("changing the status filter requests history with the new status", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: [], totalCount: 0, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);
    await userEvent.selectOptions(screen.getByLabelText("Status"), "8");

    expect(useGetHistory).toHaveBeenLastCalledWith(expect.objectContaining({ status: 8, page: 1 }));
  });

  it("shows the bulk toolbar once rows are selected and starts a ZIP export", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });
    createBulkExport.mockResolvedValue({ id: "export-1" });

    renderWithProviders(<HistoryScreen />);
    expect(screen.queryByText(/selected/i)).not.toBeInTheDocument();

    const checkboxes = screen.getAllByRole("checkbox");
    await userEvent.click(checkboxes[0]!);

    expect(screen.getByText("1 selected")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /download pdfs \(zip\)/i }));

    expect(createBulkExport).toHaveBeenCalledWith({ instanceIds: ["r1"], format: 0 });
    expect(push).toHaveBeenCalledWith("/exports/export-1");
  });

  it("starts an Excel export for the selected rows", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });
    createBulkExport.mockResolvedValue({ id: "export-2" });

    renderWithProviders(<HistoryScreen />);
    const checkboxes = screen.getAllByRole("checkbox");
    await userEvent.click(checkboxes[0]!);
    await userEvent.click(checkboxes[1]!);
    await userEvent.click(screen.getByRole("button", { name: /export to excel/i }));

    expect(createBulkExport).toHaveBeenCalledWith({ instanceIds: ["r1", "r2"], format: 1 });
    expect(push).toHaveBeenCalledWith("/exports/export-2");
  });

  it("clears the selection and hides the toolbar", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);
    await userEvent.click(screen.getAllByRole("checkbox")[0]!);
    expect(screen.getByText("1 selected")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /clear/i }));

    expect(screen.queryByText(/selected/i)).not.toBeInTheDocument();
  });

  it("downloads a single report's PDF directly from its row", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });
    getHistoryReportPdf.mockResolvedValue(new Blob(["pdf"]));

    renderWithProviders(<HistoryScreen />);
    await userEvent.click(screen.getAllByRole("button", { name: /download pdf/i })[0]!);

    expect(getHistoryReportPdf).toHaveBeenCalledWith("r1");
  });

  it("downloads a single report's Excel directly from its row", async () => {
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: ROWS, totalCount: 2, page: 1, pageSize: 20 } });
    getHistoryReportExcel.mockResolvedValue(new Blob(["xlsx"]));

    renderWithProviders(<HistoryScreen />);
    await userEvent.click(screen.getAllByRole("button", { name: /download excel/i })[0]!);

    expect(getHistoryReportExcel).toHaveBeenCalledWith("r1");
  });

  it("debounces the search box before requesting history", () => {
    vi.useFakeTimers();
    useGetHistory.mockReturnValue({ isPending: false, isError: false, data: { rows: [], totalCount: 0, page: 1, pageSize: 20 } });

    renderWithProviders(<HistoryScreen />);
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "borehole" } });

    expect(useGetHistory).not.toHaveBeenLastCalledWith(expect.objectContaining({ search: "borehole" }));

    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(useGetHistory).toHaveBeenLastCalledWith(expect.objectContaining({ search: "borehole" }));
  });
});

afterEach(() => {
  vi.useRealTimers();
});
