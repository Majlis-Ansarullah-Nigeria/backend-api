import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { AssignHeadPanel } from "./AssignHeadPanel";

const createRoleAssignmentMutate = vi.fn();

vi.mock("@/lib/api/generated/organization/organization", () => ({
  useCreateRoleAssignment: () => ({ mutate: createRoleAssignmentMutate, isPending: false, isError: false }),
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

beforeEach(() => {
  createRoleAssignmentMutate.mockReset();
});

describe("AssignHeadPanel", () => {
  it("shows the vacant-head badge and an 'assign head' button when there's no head", () => {
    renderWithProviders(<AssignHeadPanel unitId="u1" unitName="Iwo Road Muqam" headName={null} hasVacantHead />);

    expect(screen.getByText("Vacant head")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /assign head/i })).toBeInTheDocument();
  });

  it("shows the current head's name and a 'change head' button when there is one", () => {
    renderWithProviders(<AssignHeadPanel unitId="u1" unitName="Iwo Road Muqam" headName="Kehinde Bello" hasVacantHead={false} />);

    expect(screen.getByText("Kehinde Bello")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /change head/i })).toBeInTheDocument();
  });

  it("submits the assignment with the unit's id and UnitHead role", async () => {
    const user = userEvent.setup();
    renderWithProviders(<AssignHeadPanel unitId="u1" unitName="Iwo Road Muqam" headName={null} hasVacantHead />);

    await user.click(screen.getByRole("button", { name: /assign head/i }));
    await user.type(screen.getByLabelText(/^email$/i), "new.head@example.org");
    await user.type(screen.getByLabelText(/^full name$/i), "New Head");
    await user.click(screen.getByRole("button", { name: /assign and send invite/i }));

    expect(createRoleAssignmentMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        id: "u1",
        data: expect.objectContaining({ email: "new.head@example.org", name: "New Head", role: 0, delegateScope: null }),
      }),
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("shows a confirmation after a successful assignment", async () => {
    createRoleAssignmentMutate.mockImplementation((_data, options) => {
      options.onSuccess({ roleAssignmentId: "ra1", invited: false });
    });
    const user = userEvent.setup();
    renderWithProviders(<AssignHeadPanel unitId="u1" unitName="Iwo Road Muqam" headName={null} hasVacantHead />);

    await user.click(screen.getByRole("button", { name: /assign head/i }));
    await user.type(screen.getByLabelText(/^email$/i), "new.head@example.org");
    await user.type(screen.getByLabelText(/^full name$/i), "New Head");
    await user.click(screen.getByRole("button", { name: /assign and send invite/i }));

    expect(screen.getByText(/head assigned/i)).toBeInTheDocument();
  });
});
