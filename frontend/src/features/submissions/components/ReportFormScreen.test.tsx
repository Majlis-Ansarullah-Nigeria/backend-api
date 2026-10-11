import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ReportFormScreen } from "./ReportFormScreen";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

const useGetReportInstanceForEditing = vi.fn();
const useGetPullContext = vi.fn();

vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportInstanceForEditing: (...args: unknown[]) => useGetReportInstanceForEditing(...args),
  useGetPullContext: (...args: unknown[]) => useGetPullContext(...args),
  getGetMyReportsQueryKey: () => ["myReports"],
  getReportPreviousAnswers: vi.fn(),
  saveReportDraft: vi.fn(),
  submitReport: vi.fn(),
  withdrawReport: vi.fn(),
  pullChildFigures: vi.fn(),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

// REPORT_INSTANCE_STATUS: NotStarted 0, Draft 1, Submitted 2, InReview 3, Returned 4, Acknowledged 5.
const BASE_DATA = {
  id: "report-1",
  windowId: "window-1",
  unitId: "unit-1",
  unitName: "Dila Ibafo",
  reportTypeId: "rt-1",
  reportTypeName: "Dila Monthly Activities Report",
  periodLabel: "September 2026",
  templateVersionId: "tv-1",
  templateVersionNumber: 1,
  schema: { sections: [] },
  unitLevel: 2,
  answers: {},
  canSave: true,
  opensAt: "2026-09-01T00:00:00Z",
  dueAt: null,
  closesAt: "2026-10-15T00:00:00Z",
  revisionDeadline: null,
  returnComment: null,
  rowVersion: "1",
  referenceNumber: "GMR-2026-09-Z03D12",
  submittedAt: "2026-09-20T00:00:00Z",
  isNil: false,
  allowNilReport: false,
};

beforeEach(() => {
  useGetReportInstanceForEditing.mockReset();
  useGetPullContext.mockReset();
  useGetPullContext.mockReturnValue({ data: undefined, isPending: false, isError: false, refetch: vi.fn() });
});

describe("ReportFormScreen", () => {
  it("shows the Secretariat's comment and revision deadline when the report was returned", () => {
    useGetReportInstanceForEditing.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        ...BASE_DATA,
        status: 4,
        returnComment: "Please double-check the welfare figures.",
        revisionDeadline: "2026-10-22T00:00:00Z",
      },
    });

    renderWithProviders(<ReportFormScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText("Returned for revision")).toBeInTheDocument();
    expect(screen.getByText("Please double-check the welfare figures.")).toBeInTheDocument();
  });

  it("doesn't show the returned banner for a Draft report", () => {
    useGetReportInstanceForEditing.mockReturnValue({
      isPending: false,
      isError: false,
      data: { ...BASE_DATA, status: 1, returnComment: null },
    });

    renderWithProviders(<ReportFormScreen id="report-1" unitId="unit-1" />);

    expect(screen.queryByText("Returned for revision")).not.toBeInTheDocument();
  });
});
