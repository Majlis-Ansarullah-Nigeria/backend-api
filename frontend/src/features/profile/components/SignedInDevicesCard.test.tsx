import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { SignedInDevicesCard } from "./SignedInDevicesCard";

const useGetSessions = vi.fn();
const revokeSessionMutate = vi.fn();
const revokeOtherSessionsMutate = vi.fn();

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useGetSessions: (...args: unknown[]) => useGetSessions(...args),
  useRevokeSession: () => ({ mutate: revokeSessionMutate, isPending: false }),
  useRevokeOtherSessions: () => ({ mutate: revokeOtherSessionsMutate, isPending: false }),
  getGetSessionsQueryKey: () => ["getSessions"],
}));

function renderCard() {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>
        <SignedInDevicesCard />
      </QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const CURRENT_SESSION = {
  id: "11111111-1111-1111-1111-111111111111",
  createdAt: "2026-01-01T00:00:00Z",
  expiresAt: "2026-01-31T00:00:00Z",
  createdByIp: "127.0.0.1",
  userAgent: "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0 Safari/537.36",
  isCurrent: true,
};

const OTHER_SESSION = {
  id: "22222222-2222-2222-2222-222222222222",
  createdAt: "2026-01-05T00:00:00Z",
  expiresAt: "2026-01-31T00:00:00Z",
  createdByIp: "10.0.0.1",
  userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) Safari/604.1",
  isCurrent: false,
};

beforeEach(() => {
  revokeSessionMutate.mockReset();
  revokeOtherSessionsMutate.mockReset();
  useGetSessions.mockReset();
});

describe("SignedInDevicesCard", () => {
  it("shows a loading message while sessions are pending", () => {
    useGetSessions.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderCard();

    expect(screen.getByText(/loading your devices/i)).toBeInTheDocument();
  });

  it("shows an error state when sessions fail to load", () => {
    useGetSessions.mockReturnValue({ isPending: false, isError: true, refetch: vi.fn() });

    renderCard();

    expect(screen.getByText(/couldn't load your devices/i)).toBeInTheDocument();
  });

  it("marks the current session and shows sign-out for other sessions only", () => {
    useGetSessions.mockReturnValue({ isPending: false, isError: false, data: [CURRENT_SESSION, OTHER_SESSION] });

    renderCard();

    expect(screen.getByText("Current")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /sign out$/i })).toHaveLength(1);
    expect(screen.getByText(/chrome on windows/i)).toBeInTheDocument();
    expect(screen.getByText(/safari/i)).toBeInTheDocument();
  });

  it("hides 'sign out others' when there is only the current session", () => {
    useGetSessions.mockReturnValue({ isPending: false, isError: false, data: [CURRENT_SESSION] });

    renderCard();

    expect(screen.queryByRole("button", { name: /sign out others/i })).not.toBeInTheDocument();
  });

  it("revokes a specific session when its sign-out button is clicked", async () => {
    useGetSessions.mockReturnValue({ isPending: false, isError: false, data: [CURRENT_SESSION, OTHER_SESSION] });
    const user = userEvent.setup();

    renderCard();
    await user.click(screen.getByRole("button", { name: /sign out$/i }));

    expect(revokeSessionMutate).toHaveBeenCalledWith(
      { sessionId: OTHER_SESSION.id },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("revokes other sessions when 'sign out others' is clicked", async () => {
    useGetSessions.mockReturnValue({ isPending: false, isError: false, data: [CURRENT_SESSION, OTHER_SESSION] });
    const user = userEvent.setup();

    renderCard();
    await user.click(screen.getByRole("button", { name: /sign out others/i }));

    expect(revokeOtherSessionsMutate).toHaveBeenCalledWith(
      undefined,
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });
});
