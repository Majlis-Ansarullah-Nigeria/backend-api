import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

const forgotPasswordMutate = vi.fn();

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useForgotPassword: () => ({ mutate: forgotPasswordMutate, isPending: false, isError: false }),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  forgotPasswordMutate.mockReset();
});

describe("ForgotPasswordForm", () => {
  it("submits the identifier and shows a confirmation screen on success", async () => {
    const user = userEvent.setup();
    renderWithIntl(<ForgotPasswordForm />);

    await user.type(screen.getByLabelText(/^email$/i), "a.adeyemi@example.org");
    await user.click(screen.getByRole("button", { name: /send reset link/i }));

    expect(forgotPasswordMutate).toHaveBeenCalledWith(
      { data: { identifier: "a.adeyemi@example.org" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    act(() => forgotPasswordMutate.mock.calls[0][1].onSuccess());

    expect(screen.getByText(/check your email/i)).toBeInTheDocument();
    expect(screen.getByText(/a\.adeyemi@example\.org/)).toBeInTheDocument();
  });

  it("links back to sign in", () => {
    renderWithIntl(<ForgotPasswordForm />);

    expect(screen.getByRole("link", { name: /back to sign in/i })).toHaveAttribute("href", "/login");
  });
});
