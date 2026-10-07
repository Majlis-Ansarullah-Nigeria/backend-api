import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { LoginForm } from "./LoginForm";

const loginMutate = vi.fn();
const requestOtpMutate = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useLogin: () => ({ mutate: loginMutate, isPending: false, isError: false }),
  useRequestOtp: () => ({ mutate: requestOtpMutate, isPending: false, isError: false }),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  loginMutate.mockReset();
  requestOtpMutate.mockReset();
  push.mockReset();
});

describe("LoginForm", () => {
  it("starts on the password tab", () => {
    renderWithIntl(<LoginForm />);

    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
  });

  it("switches to the email-code tab and hides the password field", async () => {
    const user = userEvent.setup();
    renderWithIntl(<LoginForm />);

    await user.click(screen.getByRole("tab", { name: /email code/i }));

    expect(screen.queryByLabelText(/^password$/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /send code/i })).toBeInTheDocument();
  });

  it("submits the password form and redirects home on a plain login", async () => {
    const user = userEvent.setup();
    renderWithIntl(<LoginForm />);

    await user.type(screen.getByLabelText(/^email$/i), "a.adeyemi@example.org");
    await user.type(screen.getByLabelText(/^password$/i), "Correct-Horse-1");
    await user.click(screen.getByRole("button", { name: /^sign in$/i }));

    expect(loginMutate).toHaveBeenCalledWith(
      { data: { identifier: "a.adeyemi@example.org", password: "Correct-Horse-1" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    loginMutate.mock.calls[0][1].onSuccess({ id: "1", name: "A", email: "a.adeyemi@example.org" });
    expect(push).toHaveBeenCalledWith("/home");
  });

  it("redirects to the MFA challenge step when the login outcome requires it", async () => {
    const user = userEvent.setup();
    renderWithIntl(<LoginForm />);

    await user.type(screen.getByLabelText(/^email$/i), "a.adeyemi@example.org");
    await user.type(screen.getByLabelText(/^password$/i), "Correct-Horse-1");
    await user.click(screen.getByRole("button", { name: /^sign in$/i }));

    loginMutate.mock.calls[0][1].onSuccess({ challengeToken: "tok_123" });
    expect(push).toHaveBeenCalledWith("/login/mfa?challenge=tok_123");
  });

  it("requests an email code and navigates to the code step", async () => {
    const user = userEvent.setup();
    renderWithIntl(<LoginForm />);

    await user.click(screen.getByRole("tab", { name: /email code/i }));
    await user.type(screen.getByLabelText(/^email$/i), "a.adeyemi@example.org");
    await user.click(screen.getByRole("button", { name: /send code/i }));

    expect(requestOtpMutate).toHaveBeenCalledWith(
      { data: { identifier: "a.adeyemi@example.org" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    requestOtpMutate.mock.calls[0][1].onSuccess();
    expect(push).toHaveBeenCalledWith("/login/code?identifier=a.adeyemi%40example.org");
  });

  it("links to the forgot-password screen", () => {
    renderWithIntl(<LoginForm />);

    expect(screen.getByRole("link", { name: /forgot password/i })).toHaveAttribute("href", "/login/forgot");
  });
});
