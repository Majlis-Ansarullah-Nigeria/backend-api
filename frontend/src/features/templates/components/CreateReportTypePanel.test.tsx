import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { CreateReportTypePanel } from "./CreateReportTypePanel";

const createReportTypeMutate = vi.fn();

vi.mock("@/lib/api/generated/templates/templates", () => ({
  useCreateReportType: () => ({ mutate: createReportTypeMutate, isPending: false, isError: false }),
  getGetReportTypesQueryKey: () => ["getReportTypes"],
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
  createReportTypeMutate.mockReset();
});

describe("CreateReportTypePanel", () => {
  it("requires at least one applicable level before submitting", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CreateReportTypePanel onCancel={vi.fn()} onCreated={vi.fn()} />);

    await user.type(screen.getByLabelText(/^name$/i), "Monthly Return");
    await user.selectOptions(screen.getByLabelText(/^category$/i), "0");
    await user.selectOptions(screen.getByLabelText(/^frequency$/i), "0");
    await user.click(screen.getByRole("button", { name: /create report type/i }));

    expect(await screen.findByText(/choose at least one level/i)).toBeInTheDocument();
    expect(createReportTypeMutate).not.toHaveBeenCalled();
  });

  it("submits numeric-coded fields built from the form", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CreateReportTypePanel onCancel={vi.fn()} onCreated={vi.fn()} />);

    await user.type(screen.getByLabelText(/^name$/i), "Monthly Return");
    await user.selectOptions(screen.getByLabelText(/^category$/i), "0");
    await user.selectOptions(screen.getByLabelText(/^frequency$/i), "0");
    await user.click(screen.getByLabelText(/^muqam$/i));
    await user.click(screen.getByRole("button", { name: /create report type/i }));

    expect(createReportTypeMutate).toHaveBeenCalledWith(
      {
        data: expect.objectContaining({
          name: "Monthly Return",
          category: 0,
          frequency: 0,
          applicableLevels: [3],
          recipientLevel: 0,
          observerDepth: 1,
          allowAttachments: false,
          code: null,
        }),
      },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("sends a chosen reference code in capitals and refuses a malformed one (FR-SUB-010)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CreateReportTypePanel onCancel={vi.fn()} onCreated={vi.fn()} />);

    await user.type(screen.getByLabelText(/^name$/i), "Dila Monthly Report");
    await user.selectOptions(screen.getByLabelText(/^category$/i), "0");
    await user.selectOptions(screen.getByLabelText(/^frequency$/i), "0");
    await user.click(screen.getByLabelText(/^dila$/i));
    const code = screen.getByLabelText(/^reference code$/i);

    await user.type(code, "d-m");
    await user.click(screen.getByRole("button", { name: /create report type/i }));
    expect(screen.getByText("Use 2–6 letters or digits, starting with a letter")).toBeInTheDocument();
    expect(createReportTypeMutate).not.toHaveBeenCalled();

    await user.clear(code);
    await user.type(code, "dmr");
    await user.click(screen.getByRole("button", { name: /create report type/i }));
    expect(createReportTypeMutate).toHaveBeenCalledWith(
      { data: expect.objectContaining({ name: "Dila Monthly Report", code: "DMR" }) },
      expect.anything(),
    );
  });
});
