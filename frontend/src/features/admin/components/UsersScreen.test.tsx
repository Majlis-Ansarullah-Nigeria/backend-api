import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { USER_STATUS } from "@/features/admin/utils/users";
import { UsersScreen } from "./UsersScreen";

const useGetUsers = vi.fn();
const useGetOrganizationTree = vi.fn();

vi.mock("@/lib/api/generated/admin/admin", () => ({
  useGetUsers: (...args: unknown[]) => useGetUsers(...args),
  useDeactivateUser: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useReactivateUser: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useAdminResetMfa: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useRevokeAllSessions: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  getGetUsersQueryKey: () => ["getUsers"],
}));

vi.mock("@/lib/api/generated/organization/organization", () => ({
  useGetOrganizationTree: (...args: unknown[]) => useGetOrganizationTree(...args),
  useCreateRoleAssignment: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  getGetOrganizationTreeQueryKey: () => ["getOrganizationTree"],
}));

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

const ACTIVE_USER = {
  userId: "u1",
  invitationId: null,
  name: "Amina Yusuf",
  email: "a.yusuf@example.org",
  lastLoginAt: "2026-01-01T08:00:00Z",
  twoFactorEnabled: true,
  status: USER_STATUS.Active,
  roleAssignments: [
    { id: "r1", role: 2, delegateScope: null, unitId: "nat", unitName: "National", unitDisplayCode: "", startsAt: "2026-01-01T00:00:00Z", endsAt: null, isActive: true },
  ],
};

const INVITED_USER = {
  userId: null,
  invitationId: "i1",
  name: "Tunde Akinola",
  email: "t.akinola@example.org",
  lastLoginAt: null,
  twoFactorEnabled: false,
  status: USER_STATUS.Invited,
  roleAssignments: [],
};

beforeEach(() => {
  useGetUsers.mockReset();
  useGetOrganizationTree.mockReset();
  useGetOrganizationTree.mockReturnValue({ isPending: false, isError: false, data: [] });
});

describe("UsersScreen", () => {
  it("shows a loading message while the list loads", () => {
    useGetUsers.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<UsersScreen />);

    expect(screen.getByText(/loading users/i)).toBeInTheDocument();
  });

  it("shows an error state with retry on failure", async () => {
    const refetch = vi.fn();
    useGetUsers.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });

    renderWithProviders(<UsersScreen />);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  it("lists both active users and pending invitations", () => {
    useGetUsers.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_USER, INVITED_USER] });

    renderWithProviders(<UsersScreen />);

    expect(screen.getByText("Amina Yusuf")).toBeInTheDocument();
    expect(screen.getByText("Tunde Akinola")).toBeInTheDocument();
    expect(screen.getByText("Invited")).toBeInTheDocument();
  });

  it("filters by search text, including by unit name", async () => {
    useGetUsers.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_USER, INVITED_USER] });
    const user = userEvent.setup();

    renderWithProviders(<UsersScreen />);
    await user.type(screen.getByRole("searchbox", { name: /name, email or unit/i }), "Tunde");

    expect(screen.getByText("Tunde Akinola")).toBeInTheDocument();
    expect(screen.queryByText("Amina Yusuf")).not.toBeInTheDocument();
  });

  it("shows the detail panel for the selected person", async () => {
    useGetUsers.mockReturnValue({ isPending: false, isError: false, data: [ACTIVE_USER] });
    const user = userEvent.setup();

    renderWithProviders(<UsersScreen />);
    await user.click(screen.getByText("Amina Yusuf"));

    expect(screen.getByRole("button", { name: /deactivate/i })).toBeInTheDocument();
    expect(screen.getAllByText("Amina Yusuf")).toHaveLength(2);
  });
});
