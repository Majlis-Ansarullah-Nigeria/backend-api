import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api/client";
import messages from "../../../../messages/en.json";
import { ChildReportScreen } from "./ChildReportScreen";

const useGetChildReport = vi.fn();
const getChildReportPdf = vi.fn();
const useGetPrivateNotes = vi.fn();
const addPrivateNote = vi.fn();

vi.mock("@/lib/api/generated/observer/observer", () => ({
  useGetChildReport: (...args: unknown[]) => useGetChildReport(...args),
  getChildReportPdf: (...args: unknown[]) => getChildReportPdf(...args),
  useGetPrivateNotes: (...args: unknown[]) => useGetPrivateNotes(...args),
  addPrivateNote: (...args: unknown[]) => addPrivateNote(...args),
  getGetPrivateNotesQueryKey: (unitId: string, reportId: string) => ["getPrivateNotes", unitId, reportId],
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

const VIEW = {
  reportId: "ri-1",
  status: 2, // Submitted
  isLate: false,
  submittedAt: "2026-10-03T18:20:00Z",
  submittedByName: "F. Lawal",
  receipt: {
    reportTypeName: "General Monthly Report",
    periodLabel: "September 2026",
    unitName: "Challenge Muqam",
    referenceNumber: "GMR-2026-09-Z03D12M04",
    generatedAt: "2026-10-03T18:20:00Z",
    sections: [
      {
        title: "Welfare",
        fields: [
          { label: "Notes", value: "All quiet", isMasked: false },
          { label: "Confidential details", value: "Hidden from observers", isMasked: true },
        ],
      },
    ],
  },
};

beforeEach(() => {
  useGetChildReport.mockReset();
  getChildReportPdf.mockReset();
  useGetPrivateNotes.mockReset();
  useGetPrivateNotes.mockReturnValue({ isPending: false, isError: false, data: [] });
  addPrivateNote.mockReset();
});

describe("ChildReportScreen", () => {
  it("shows a loading message while the report loads", () => {
    useGetChildReport.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<ChildReportScreen unitId="m-1" reportId="ri-1" />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows a 'not submitted yet' state for a draft report", () => {
    useGetChildReport.mockReturnValue({
      isPending: false,
      isError: true,
      error: new ApiError(409, "NOT_SUBMITTED", "This unit hasn't submitted this report yet."),
      data: undefined,
    });

    renderWithProviders(<ChildReportScreen unitId="m-1" reportId="ri-1" />);

    expect(screen.getByText(/not submitted yet/i)).toBeInTheDocument();
    expect(screen.getByText(/hasn't submitted this report yet/i)).toBeInTheDocument();
  });

  it("shows a generic error state with retry for other failures", async () => {
    const refetch = vi.fn();
    useGetChildReport.mockReturnValue({
      isPending: false,
      isError: true,
      error: new ApiError(403, "FORBIDDEN", "You don't observe that unit's reports."),
      data: undefined,
      refetch,
    });
    const user = userEvent.setup();

    renderWithProviders(<ChildReportScreen unitId="m-1" reportId="ri-1" />);
    await user.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("renders sections with the confidential value masked and the plain value shown", () => {
    useGetChildReport.mockReturnValue({ isPending: false, isError: false, data: VIEW });

    renderWithProviders(<ChildReportScreen unitId="m-1" reportId="ri-1" />);

    expect(screen.getByText("Challenge Muqam · September 2026")).toBeInTheDocument();
    expect(screen.getByText(/submitted.*by F\. Lawal/i)).toBeInTheDocument();
    expect(screen.getByText("All quiet")).toBeInTheDocument();
    expect(screen.getByText("Hidden from observers")).toBeInTheDocument();
  });

  it("downloads the PDF on button click", async () => {
    useGetChildReport.mockReturnValue({ isPending: false, isError: false, data: VIEW });
    getChildReportPdf.mockResolvedValue(new Blob());
    const user = userEvent.setup();

    renderWithProviders(<ChildReportScreen unitId="m-1" reportId="ri-1" />);
    await user.click(screen.getByRole("button", { name: /download pdf/i }));

    expect(getChildReportPdf).toHaveBeenCalledWith("m-1", "ri-1");
  });
});
