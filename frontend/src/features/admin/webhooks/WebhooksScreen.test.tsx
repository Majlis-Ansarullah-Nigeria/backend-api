import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { WebhooksScreen } from "./WebhooksScreen";

const useGetWebhooks = vi.fn();
const useGetWebhookDeliveries = vi.fn();
const createMutate = vi.fn();
const updateMutate = vi.fn();
const deleteMutate = vi.fn();
const rotateMutate = vi.fn();
const testMutate = vi.fn();

vi.mock("@/lib/api/generated/settings/settings", () => ({
  useGetWebhooks: (...args: unknown[]) => useGetWebhooks(...args),
  useGetWebhookDeliveries: (...args: unknown[]) => useGetWebhookDeliveries(...args),
  useCreateWebhook: () => ({ mutate: createMutate, isPending: false }),
  useUpdateWebhook: () => ({ mutate: updateMutate, isPending: false }),
  useDeleteWebhook: () => ({ mutate: deleteMutate, isPending: false }),
  useRotateWebhookSecret: () => ({ mutate: rotateMutate, isPending: false }),
  useSendTestWebhook: () => ({ mutate: testMutate, isPending: false }),
  getGetWebhooksQueryKey: () => ["webhooks"],
  getGetWebhookDeliveriesQueryKey: (id: string) => ["deliveries", id],
}));

function renderWithProviders(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const ENDPOINT = {
  id: "h1",
  url: "https://hooks.example.org/hrs",
  events: [0, 1],
  isActive: true,
  secretHint: "…ab12",
  createdAt: "2026-10-20T09:00:00Z",
  deliveredLast24Hours: 3,
  pending: 1,
  failed: 2,
  lastDeliveryAt: null,
  lastDeliveryStatus: null,
};

beforeEach(() => {
  for (const mock of [createMutate, updateMutate, deleteMutate, rotateMutate, testMutate]) {
    mock.mockReset();
  }
  useGetWebhooks.mockReturnValue({ isPending: false, isError: false, isSuccess: true, data: [ENDPOINT] });
  useGetWebhookDeliveries.mockReturnValue({
    isPending: false,
    isError: false,
    data: [
      {
        id: "d1",
        event: 0,
        status: 0,
        attempts: 1,
        lastStatusCode: 500,
        lastError: "Internal Server Error",
        createdAt: "2026-10-24T08:00:00Z",
        deliveredAt: null,
        nextAttemptAt: "2026-10-24T08:01:00Z",
      },
    ],
  });
});

describe("WebhooksScreen", () => {
  it("lists endpoints with their events, secret hint and delivery counts", () => {
    renderWithProviders(<WebhooksScreen />);

    expect(screen.getByText("https://hooks.example.org/hrs")).toBeInTheDocument();
    expect(screen.getByText("report.submitted, report.acknowledged · secret …ab12")).toBeInTheDocument();
    expect(screen.getByText(/3 delivered in 24 h · 1 pending/)).toBeInTheDocument();
    expect(screen.getByText("· 2 failed")).toBeInTheDocument();
  });

  it("refuses a non-HTTPS address, then creates an endpoint and shows its secret once", async () => {
    createMutate.mockImplementation((_vars, options: { onSuccess: (result: unknown) => void }) =>
      options.onSuccess({ endpoint: { ...ENDPOINT, id: "h2", url: "https://example.org/in" }, secret: "whsec_new" }),
    );
    renderWithProviders(<WebhooksScreen />);

    await userEvent.click(screen.getByRole("button", { name: "Add endpoint" }));
    const url = screen.getByLabelText("Endpoint URL");
    await userEvent.type(url, "http://example.org/in");
    await userEvent.click(screen.getByRole("button", { name: "Create endpoint" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Enter an address that starts with https://");
    expect(createMutate).not.toHaveBeenCalled();

    await userEvent.clear(url);
    await userEvent.type(url, "https://example.org/in");
    await userEvent.click(screen.getByRole("checkbox", { name: /report\.acknowledged/ }));
    await userEvent.click(screen.getByRole("button", { name: "Create endpoint" }));

    expect(createMutate).toHaveBeenCalledWith({ data: { url: "https://example.org/in", events: [0, 1], isActive: true } }, expect.anything());
    expect(screen.getByText("whsec_new")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "I've saved it" }));
    expect(screen.queryByText("whsec_new")).not.toBeInTheDocument();
  });

  it("asks before rotating the secret or deleting, and sends a test event", async () => {
    rotateMutate.mockImplementation((_vars, options: { onSuccess: (result: unknown) => void }) =>
      options.onSuccess({ endpoint: ENDPOINT, secret: "whsec_rotated" }),
    );
    renderWithProviders(<WebhooksScreen />);

    await userEvent.click(screen.getByRole("button", { name: "Send test" }));
    expect(testMutate).toHaveBeenCalledWith({ id: "h1" }, expect.anything());

    await userEvent.click(screen.getByRole("button", { name: "Rotate secret" }));
    expect(rotateMutate).not.toHaveBeenCalled();
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Rotate" }));
    expect(rotateMutate).toHaveBeenCalledWith({ id: "h1" }, expect.anything());
    expect(screen.getByText("whsec_rotated")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Cancel" }));
    expect(deleteMutate).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Delete" }));
    expect(deleteMutate).toHaveBeenCalledWith({ id: "h1" }, expect.anything());
  });

  it("shows the delivery log with retries", async () => {
    renderWithProviders(<WebhooksScreen />);

    await userEvent.click(screen.getByRole("button", { name: "Deliveries" }));

    const log = screen.getByRole("heading", { name: "Deliveries to https://hooks.example.org/hrs" }).closest("section")!;
    expect(within(log).getByText("report.submitted")).toBeInTheDocument();
    expect(within(log).getByText(/^Retrying at/)).toBeInTheDocument();
    expect(within(log).getByText("HTTP 500")).toBeInTheDocument();
  });

  it("explains that only the system administrator manages webhooks", () => {
    useGetWebhooks.mockReturnValue({ isPending: false, isError: true, isSuccess: false, error: { status: 403 } });
    renderWithProviders(<WebhooksScreen />);

    expect(screen.getByText("Only the system administrator manages webhooks")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add endpoint" })).not.toBeInTheDocument();
  });
});
