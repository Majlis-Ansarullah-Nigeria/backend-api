import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { InboxScreen } from "./InboxScreen";

const useGetInbox = vi.fn();
const useGetInboxReportDetail = vi.fn();
const useGetSavedViews = vi.fn();
const useCreateSavedView = vi.fn();
const useDeleteSavedView = vi.fn();
const useOpenInboxReport = vi.fn();
const useAcknowledgeInboxReport = vi.fn();
const useReturnInboxReport = vi.fn();
const useSetInboxReportFlagged = vi.fn();
const getGetInboxQueryKey = (params?: unknown) => ["getInbox", params];
const getGetInboxReportDetailQueryKey = (id: string) => ["getInboxReportDetail", id];

vi.mock("@/lib/api/generated/review/review", () => ({
  useGetInbox: (...args: unknown[]) => useGetInbox(...args),
  useGetInboxReportDetail: (...args: unknown[]) => useGetInboxReportDetail(...args),
  useGetSavedViews: (...args: unknown[]) => useGetSavedViews(...args),
  useCreateSavedView: (...args: unknown[]) => useCreateSavedView(...args),
  useDeleteSavedView: (...args: unknown[]) => useDeleteSavedView(...args),
  useOpenInboxReport: (...args: unknown[]) => useOpenInboxReport(...args),
  useAcknowledgeInboxReport: (...args: unknown[]) => useAcknowledgeInboxReport(...args),
  useReturnInboxReport: (...args: unknown[]) => useReturnInboxReport(...args),
  useSetInboxReportFlagged: (...args: unknown[]) => useSetInboxReportFlagged(...args),
  getGetInboxQueryKey: (params?: unknown) => getGetInboxQueryKey(params),
  getGetInboxReportDetailQueryKey: (id: string) => getGetInboxReportDetailQueryKey(id),
}));

const useGetReportTypes = vi.fn();
vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportTypes: (...args: unknown[]) => useGetReportTypes(...args),
}));

const useGetSubmissionWindows = vi.fn();
vi.mock("@/lib/api/generated/windows/windows", () => ({
  useGetSubmissionWindows: (...args: unknown[]) => useGetSubmissionWindows(...args),
}));

const useGetUnitLevels = vi.fn();
const useGetUnits = vi.fn();
vi.mock("@/lib/api/generated/organization/organization", () => ({
  useGetUnitLevels: (...args: unknown[]) => useGetUnitLevels(...args),
  useGetUnits: (...args: unknown[]) => useGetUnits(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const INBOX_ITEM = {
  reportId: "r1",
  reportTypeId: "rt1",
  reportTypeName: "Dila Monthly Activities Report",
  unitId: "u1",
  unitName: "Dila Ibafo",
  unitLevel: 2,
  zoneUnitId: "z1",
  zoneName: "Zone 3",
  windowId: "w1",
  periodLabel: "September 2026",
  status: 2,
  isLate: false,
  isFlagged: false,
  submittedAt: "2026-10-01T10:00:00Z",
  referenceNumber: "GMR-2026-09-Z03D12",
};

beforeEach(() => {
  useGetInbox.mockReset();
  useGetInboxReportDetail.mockReset();
  useGetSavedViews.mockReset();
  useCreateSavedView.mockReset();
  useDeleteSavedView.mockReset();
  useOpenInboxReport.mockReset();
  useAcknowledgeInboxReport.mockReset();
  useReturnInboxReport.mockReset();
  useSetInboxReportFlagged.mockReset();
  useGetReportTypes.mockReset();
  useGetSubmissionWindows.mockReset();
  useGetUnitLevels.mockReset();
  useGetUnits.mockReset();

  useGetReportTypes.mockReturnValue({ data: [] });
  useGetSubmissionWindows.mockReturnValue({ data: [] });
  useGetUnitLevels.mockReturnValue({ data: [] });
  useGetUnits.mockReturnValue({ data: [] });
  useGetSavedViews.mockReturnValue({ data: [], refetch: vi.fn() });
  useCreateSavedView.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useDeleteSavedView.mockReturnValue({ mutate: vi.fn() });
  useOpenInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false });
  useAcknowledgeInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
  useReturnInboxReport.mockReturnValue({ mutate: vi.fn(), isPending: false, error: null });
  useSetInboxReportFlagged.mockReturnValue({ mutate: vi.fn(), isPending: false });
});

describe("InboxScreen", () => {
  it("shows a loading message while the inbox loads", () => {
    useGetInbox.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetInbox.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an empty state when nothing matches the filter", () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText(/nothing in the inbox/i)).toBeInTheDocument();
  });

  it("lists inbox items and shows their status and unit/zone", () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [INBOX_ITEM] });

    renderWithProviders(<InboxScreen />);

    expect(screen.getByText("Dila Monthly Activities Report")).toBeInTheDocument();
    expect(screen.getByText(/Dila Ibafo/)).toBeInTheDocument();
    expect(screen.getByText(/Zone 3/)).toBeInTheDocument();
  });

  it("selecting a row shows the read-only report content and no download/comment/assign actions", async () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [INBOX_ITEM] });
    useGetInboxReportDetail.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        status: 2,
        isLate: false,
        isFlagged: false,
        submittedAt: "2026-10-01T10:00:00Z",
        returnComment: null,
        receipt: {
          reportTypeName: "Dila Monthly Activities Report",
          periodLabel: "September 2026",
          unitName: "Dila Ibafo",
          referenceNumber: "GMR-2026-09-Z03D12",
          generatedAt: "2026-10-01T10:00:00Z",
          sections: [{ title: "Umumi", fields: [{ label: "Tajnid", value: "120", isMasked: false }] }],
        },
      },
    });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByText("Dila Monthly Activities Report"));

    expect(screen.getByText("Umumi")).toBeInTheDocument();
    expect(screen.getByText("Tajnid")).toBeInTheDocument();
    // Acknowledge/Return/Flag (M7.2) are in scope now; download/comment/assign (M7.3+) are not.
    expect(screen.queryByRole("button", { name: /download|comment|assign/i })).not.toBeInTheDocument();
  });

  it("switching to the Flagged tab filters by isFlagged instead of status", async () => {
    useGetInbox.mockReturnValue({ isPending: false, isError: false, data: [] });

    renderWithProviders(<InboxScreen />);
    await userEvent.click(screen.getByRole("tab", { name: "Flagged" }));

    const lastCall = useGetInbox.mock.calls.at(-1);
    expect(lastCall?.[0]).toMatchObject({ isFlagged: true, status: undefined });
  });

  it("flagged and late items show their chips in the list", () => {
    useGetInbox.mockReturnValue({
      isPending: false,
      isError: false,
      data: [{ ...INBOX_ITEM, isLate: true, isFlagged: true }],
    });

    renderWithProviders(<InboxScreen />);

    const row = screen.getByText("Dila Monthly Activities Report").closest("tr");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("Late")).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText("Flagged")).toBeInTheDocument();
  });
});
