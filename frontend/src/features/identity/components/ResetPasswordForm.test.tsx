import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ResetPasswordForm } from "./ResetPasswordForm";

const resetPasswordMutate = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useSearchParams: () => new URLSearchParams({ email: "a.adeyemi@example.org" }),
}));

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useResetPassword: () => ({ mutate: resetPasswordMutate, isPending: false, isError: false }),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  resetPasswordMutate.mockReset();
  push.mockReset();
});

describe("ResetPasswordForm", () => {
  it("shows password criteria as the user types", async () => {
    const user = userEvent.setup();
    renderWithIntl(<ResetPasswordForm token="abc123" />);

    await user.type(screen.getByLabelText(/new password/i), "Short1");

    // Too short, but has a digit: the digit criterion should read as met.
    const digitCriterion = screen.getByText(/a number/i);
    expect(digitCriterion.closest("li")).toHaveClass("text-green-700");
  });

  it("submits the token, email and new password", async () => {
    const user = userEvent.setup();
    renderWithIntl(<ResetPasswordForm token="abc123" />);

    await user.type(screen.getByLabelText(/new password/i), "Correct-Horse-New1");
    await user.click(screen.getByRole("button", { name: /^reset password$/i }));

    expect(resetPasswordMutate).toHaveBeenCalledWith(
      { data: { email: "a.adeyemi@example.org", token: "abc123", newPassword: "Correct-Horse-New1" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("shows a done state with a link back to sign in after a successful reset", async () => {
    const user = userEvent.setup();
    renderWithIntl(<ResetPasswordForm token="abc123" />);

    await user.type(screen.getByLabelText(/new password/i), "Correct-Horse-New1");
    await user.click(screen.getByRole("button", { name: /^reset password$/i }));
    act(() => resetPasswordMutate.mock.calls[0][1].onSuccess());

    expect(screen.getByText(/password updated/i)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /back to sign in/i }));
    expect(push).toHaveBeenCalledWith("/login");
  });
});
