import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithIntl } from "@/test/renderWithIntl";
import { AcceptInvitationForm } from "./AcceptInvitationForm";

const push = vi.fn();
const mutate = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

const useGetInvitation = vi.fn();
const useAcceptInvitation = vi.fn();

vi.mock("@/lib/api/generated/invitations/invitations", () => ({
  useGetInvitation: (...args: unknown[]) => useGetInvitation(...args),
}));

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useAcceptInvitation: (...args: unknown[]) => useAcceptInvitation(...args),
}));

const VALID_PREVIEW = {
  isPending: false,
  isError: false,
  data: { name: "Abdulrahman Adeyemi", email: "a.adeyemi@example.org", expiresAt: "2026-10-11T00:00:00Z" },
};

beforeEach(() => {
  push.mockReset();
  mutate.mockReset();
  useGetInvitation.mockReset();
  useAcceptInvitation.mockReturnValue({ mutate, isPending: false, isError: false });
});

describe("AcceptInvitationForm", () => {
  it("shows a loading message while the invitation preview is pending", () => {
    useGetInvitation.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithIntl(<AcceptInvitationForm token="abc" />);

    expect(screen.getByText(/checking your invitation/i)).toBeInTheDocument();
  });

  it("shows an expired message when the invitation has expired", () => {
    useGetInvitation.mockReturnValue({
      isPending: false,
      isError: true,
      error: { code: "INVITE_EXPIRED", message: "expired" },
    });

    renderWithIntl(<AcceptInvitationForm token="abc" />);

    expect(screen.getByRole("alert")).toHaveTextContent(/expired/i);
  });

  it("shows a not-found message for an unknown error code", () => {
    useGetInvitation.mockReturnValue({
      isPending: false,
      isError: true,
      error: { code: "SOMETHING_ELSE", message: "boom" },
    });

    renderWithIntl(<AcceptInvitationForm token="abc" />);

    expect(screen.getByText(/invitation link isn't valid/i)).toBeInTheDocument();
  });

  it("prefills the name and shows the invitation's email, disabled", () => {
    useGetInvitation.mockReturnValue(VALID_PREVIEW);

    renderWithIntl(<AcceptInvitationForm token="abc" />);

    expect(screen.getByLabelText(/full name/i)).toHaveValue("Abdulrahman Adeyemi");
    expect(screen.getByLabelText(/^email$/i)).toHaveValue("a.adeyemi@example.org");
    expect(screen.getByLabelText(/^email$/i)).toBeDisabled();
  });

  it("updates the password strength chips as the user types", async () => {
    useGetInvitation.mockReturnValue(VALID_PREVIEW);
    const user = userEvent.setup();

    renderWithIntl(<AcceptInvitationForm token="abc" />);
    await user.type(screen.getByLabelText(/create password/i), "short1");

    const lengthChip = screen.getByText("12+ characters");
    expect(lengthChip.closest("li")).toHaveClass("text-ink-muted");

    await user.type(screen.getByLabelText(/create password/i), "2345678");
    expect(lengthChip.closest("li")).toHaveClass("text-green-700");
  });

  it("blocks submission and shows an inline error when the privacy notice isn't accepted", async () => {
    useGetInvitation.mockReturnValue(VALID_PREVIEW);
    const user = userEvent.setup();

    renderWithIntl(<AcceptInvitationForm token="abc" />);
    await user.type(screen.getByLabelText(/create password/i), "a-perfectly-fine-password1");
    await user.click(screen.getByRole("button", { name: /create account and continue/i }));

    expect(await screen.findByText(/must agree to the privacy notice/i)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it("submits the form and redirects into the app on success", async () => {
    useGetInvitation.mockReturnValue(VALID_PREVIEW);
    mutate.mockImplementation((_variables, { onSuccess }: { onSuccess: () => void }) => onSuccess());
    const user = userEvent.setup();

    renderWithIntl(<AcceptInvitationForm token="invite-token" />);
    await user.type(screen.getByLabelText(/create password/i), "a-perfectly-fine-password1");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /create account and continue/i }));

    await waitFor(() => {
      expect(mutate).toHaveBeenCalledWith(
        {
          token: "invite-token",
          data: {
            name: "Abdulrahman Adeyemi",
            phone: null,
            password: "a-perfectly-fine-password1",
            agreedToPrivacyNotice: true,
          },
        },
        expect.objectContaining({ onSuccess: expect.any(Function) }),
      );
    });
    expect(push).toHaveBeenCalledWith("/");
  });

  it("shows the server error message when accepting fails", () => {
    useGetInvitation.mockReturnValue(VALID_PREVIEW);
    useAcceptInvitation.mockReturnValue({
      mutate,
      isPending: false,
      isError: true,
      error: { code: "USER_ALREADY_EXISTS", message: "An account with this email already exists." },
    });

    renderWithIntl(<AcceptInvitationForm token="abc" />);

    expect(screen.getByRole("alert")).toHaveTextContent("An account with this email already exists.");
  });
});
