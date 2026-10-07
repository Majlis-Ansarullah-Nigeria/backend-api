import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ReportTypesScreen } from "./ReportTypesScreen";

const useGetReportTypes = vi.fn();
const useGetReportType = vi.fn();

vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportTypes: (...args: unknown[]) => useGetReportTypes(...args),
  useGetReportType: (...args: unknown[]) => useGetReportType(...args),
  useCreateReportType: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useUpdateReportType: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useArchiveReportType: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useRestoreReportType: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  getGetReportTypesQueryKey: () => ["getReportTypes"],
  getGetReportTypeQueryKey: (id: string) => ["getReportType", id],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const ACTIVE_TYPE = {
  id: "rt1",
  name: "General Monthly Report",
  description: null,
  category: 0,
  frequency: 0,
  applicableLevels: [3],
  recipientLevel: 0,
  observerDepth: 1,
  allowAttachments: true,
  createdAt: "2026-01-01T00:00:00Z",
  archivedAt: null,
};

const ARCHIVED_TYPE = {
  id: "rt2",
  name: "Retired Report",
  description: null,
  category: 3,
  frequency: 3,
  applicableLevels: [1],
  recipientLevel: 0,
  observerDepth: 0,
  allowAttachments: false,
  createdAt: "2026-01-01T00:00:00Z",
  archivedAt: "2026-02-01T00:00:00Z",
};

beforeEach(() => {
  useGetReportTypes.mockReset();
  useGetReportType.mockReset();
  useGetReportType.mockReturnValue({ isPending: true, isError: false, data: undefined });
});

describe("ReportTypesScreen", () => {
  it("shows a loading message while the list loads", () => {
    useGetReportTypes.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<ReportTypesScreen />);

    expect(screen.getByText(/loading report types/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetReportTypes.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<ReportTypesScreen />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows active report types by default, not archived ones", () => {
    useGetReportTypes.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_TYPE, ARCHIVED_TYPE] });

    renderWithProviders(<ReportTypesScreen />);

    expect(screen.getByText("General Monthly Report")).toBeInTheDocument();
    expect(screen.queryByText("Retired Report")).not.toBeInTheDocument();
  });

  it("switches to the archived tab", async () => {
    useGetReportTypes.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_TYPE, ARCHIVED_TYPE] });
    const user = userEvent.setup();

    renderWithProviders(<ReportTypesScreen />);
    await user.click(screen.getByRole("tab", { name: /archived/i }));

    expect(screen.getByText("Retired Report")).toBeInTheDocument();
    expect(screen.queryByText("General Monthly Report")).not.toBeInTheDocument();
  });

  it("opens the detail panel when a row is selected", async () => {
    useGetReportTypes.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_TYPE] });
    const user = userEvent.setup();

    renderWithProviders(<ReportTypesScreen />);
    await user.click(screen.getByText("General Monthly Report"));

    expect(screen.getByText(/loading report type/i)).toBeInTheDocument();
  });

  it("opens the create panel from the 'new report type' button", async () => {
    useGetReportTypes.mockReturnValue({ isPending: false, isError: false, data: [] });
    const user = userEvent.setup();

    renderWithProviders(<ReportTypesScreen />);
    await user.click(screen.getByRole("button", { name: /new report type/i }));

    expect(screen.getByLabelText(/^name$/i)).toBeInTheDocument();
  });
});
