import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ExportStatusScreen } from "./ExportStatusScreen";

const useGetExport = vi.fn();
const getExportDownloadUrl = vi.fn();

vi.mock("@/lib/api/generated/exports/exports", () => ({
  useGetExport: (...args: unknown[]) => useGetExport(...args),
  getExportDownloadUrl: (...args: unknown[]) => getExportDownloadUrl(...args),
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetExport.mockReset();
  getExportDownloadUrl.mockReset();
});

describe("ExportStatusScreen", () => {
  it("shows a loading message while the export status loads", () => {
    useGetExport.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<ExportStatusScreen id="export-1" />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetExport.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<ExportStatusScreen id="export-1" />);
    await userEvent.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("shows an in-progress message while pending or processing", () => {
    useGetExport.mockReturnValue({
      isPending: false,
      isError: false,
      data: { id: "export-1", format: 1, status: 1, reportCount: "0", errorMessage: null, createdAt: "2026-10-01T00:00:00Z" },
    });

    renderWithProviders(<ExportStatusScreen id="export-1" />);

    expect(screen.getByText(/preparing your export/i)).toBeInTheDocument();
  });

  it("offers a download once the export is ready", async () => {
    useGetExport.mockReturnValue({
      isPending: false,
      isError: false,
      data: { id: "export-1", format: 1, status: 2, reportCount: "258", errorMessage: null, createdAt: "2026-10-01T00:00:00Z" },
    });
    getExportDownloadUrl.mockResolvedValue({ url: "https://fake-storage.test/exports/export-1.xlsx" });

    const assign = vi.fn();
    Object.defineProperty(window, "location", { value: { assign }, writable: true });

    renderWithProviders(<ExportStatusScreen id="export-1" />);
    expect(screen.getByText(/258 report/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /^download$/i }));

    expect(getExportDownloadUrl).toHaveBeenCalledWith("export-1");
    expect(assign).toHaveBeenCalledWith("https://fake-storage.test/exports/export-1.xlsx");
  });

  it("shows the failure message when the export failed", () => {
    useGetExport.mockReturnValue({
      isPending: false,
      isError: false,
      data: { id: "export-1", format: 0, status: 3, reportCount: "0", errorMessage: "None of the selected reports could be found.", createdAt: "2026-10-01T00:00:00Z" },
    });

    renderWithProviders(<ExportStatusScreen id="export-1" />);

    expect(screen.getByText(/this export failed/i)).toBeInTheDocument();
    expect(screen.getByText("None of the selected reports could be found.")).toBeInTheDocument();
  });
});
