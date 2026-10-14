import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { SecretariatExportsScreen } from "./SecretariatExportsScreen";

const useGetMyExports = vi.fn();
const createOrganizationExport = vi.fn();
const useGetReportTypes = vi.fn();
const useGetSubmissionWindows = vi.fn();

vi.mock("@/lib/api/generated/exports/exports", () => ({
  useGetMyExports: (...args: unknown[]) => useGetMyExports(...args),
  createOrganizationExport: (...args: unknown[]) => createOrganizationExport(...args),
}));

vi.mock("@/lib/api/generated/templates/templates", () => ({
  useGetReportTypes: (...args: unknown[]) => useGetReportTypes(...args),
}));

vi.mock("@/lib/api/generated/windows/windows", () => ({
  useGetSubmissionWindows: (...args: unknown[]) => useGetSubmissionWindows(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const EXPORTS = [
  {
    id: "export-1",
    requestedByUserId: "u1",
    requestedByName: "Mariam Okafor",
    format: 1,
    status: 2,
    reportCount: 258,
    errorMessage: null,
    createdAt: "2026-10-01T09:00:00Z",
    completedAt: "2026-10-01T09:05:00Z",
  },
];

beforeEach(() => {
  useGetMyExports.mockReset();
  createOrganizationExport.mockReset();
  useGetReportTypes.mockReset();
  useGetSubmissionWindows.mockReset();
  useGetReportTypes.mockReturnValue({ data: [{ id: "t1", name: "General Monthly Report" }] });
  useGetSubmissionWindows.mockReturnValue({ data: [] });
  useGetMyExports.mockReturnValue({ isPending: false, isError: false, data: [], refetch: vi.fn() });
});

describe("SecretariatExportsScreen", () => {
  it("defaults to the Excel workbook format and starts an export on submit", async () => {
    createOrganizationExport.mockResolvedValue({ id: "export-9" });

    renderWithProviders(<SecretariatExportsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /start export/i }));

    expect(createOrganizationExport).toHaveBeenCalledWith(
      expect.objectContaining({ format: 1, reportTypeId: null, fromWindowId: null, toWindowId: null, status: null }),
    );
    expect(await screen.findByText(/view progress/i)).toHaveAttribute("href", "/exports/export-9");
  });

  it("starts a ZIP-of-PDFs export when that format is chosen", async () => {
    createOrganizationExport.mockResolvedValue({ id: "export-10" });

    renderWithProviders(<SecretariatExportsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /pdfs in a zip/i }));
    await userEvent.click(screen.getByRole("button", { name: /start export/i }));

    expect(createOrganizationExport).toHaveBeenCalledWith(expect.objectContaining({ format: 0 }));
  });

  it("shows an error message when starting an export fails", async () => {
    createOrganizationExport.mockRejectedValue(new Error("boom"));

    renderWithProviders(<SecretariatExportsScreen />);
    await userEvent.click(screen.getByRole("button", { name: /start export/i }));

    expect(await screen.findByText(/couldn't start that export/i)).toBeInTheDocument();
  });

  it("shows an empty state when there are no recent exports", () => {
    renderWithProviders(<SecretariatExportsScreen />);

    expect(screen.getByText(/no exports yet/i)).toBeInTheDocument();
  });

  it("lists recent exports with their format, status and requester", () => {
    useGetMyExports.mockReturnValue({ isPending: false, isError: false, data: EXPORTS, refetch: vi.fn() });

    renderWithProviders(<SecretariatExportsScreen />);

    const table = within(screen.getByRole("table"));
    expect(table.getByText("Excel workbook")).toBeInTheDocument();
    expect(table.getByText("Ready")).toBeInTheDocument();
    expect(table.getByText("Mariam Okafor")).toBeInTheDocument();
  });
});
