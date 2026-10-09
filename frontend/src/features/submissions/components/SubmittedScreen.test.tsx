import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { SubmittedScreen } from "./SubmittedScreen";

const useGetReportInstanceForEditing = vi.fn();

vi.mock("@/lib/api/generated/submissions/submissions", () => ({
  useGetReportInstanceForEditing: (...args: unknown[]) => useGetReportInstanceForEditing(...args),
  getReportReceiptPdf: vi.fn(),
}));

vi.mock("@/features/organization/utils/bulk-import", () => ({
  downloadBlob: vi.fn(),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetReportInstanceForEditing.mockReset();
});

describe("SubmittedScreen", () => {
  it("shows a loading message while the report loads", () => {
    useGetReportInstanceForEditing.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithIntl(<SubmittedScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows the reference number, submitted time and status", () => {
    useGetReportInstanceForEditing.mockReturnValue({
      isPending: false,
      isError: false,
      data: {
        periodLabel: "September 2026",
        referenceNumber: "GMR-2026-09-ABC123",
        submittedAt: "2026-10-04T10:42:00Z",
        status: 2, // Submitted
      },
    });

    renderWithIntl(<SubmittedScreen id="report-1" unitId="unit-1" />);

    expect(screen.getByText("GMR-2026-09-ABC123")).toBeInTheDocument();
    expect(screen.getByText(/report submitted/i)).toBeInTheDocument();
  });
});
