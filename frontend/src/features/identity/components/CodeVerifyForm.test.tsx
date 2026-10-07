import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { CodeVerifyForm } from "./CodeVerifyForm";

const verifyOtpMutate = vi.fn();
const requestOtpMutate = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useSearchParams: () => new URLSearchParams({ identifier: "a.adeyemi@example.org" }),
}));

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useVerifyOtp: () => ({ mutate: verifyOtpMutate, isPending: false, isError: false }),
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
  verifyOtpMutate.mockReset();
  requestOtpMutate.mockReset();
  push.mockReset();
});

describe("CodeVerifyForm", () => {
  it("shows the masked email it sent the code to", () => {
    renderWithIntl(<CodeVerifyForm />);

    expect(screen.getByText(/a\.ade…@example\.org/)).toBeInTheDocument();
  });

  it("keeps verify disabled until all 6 digits are entered", async () => {
    const user = userEvent.setup();
    renderWithIntl(<CodeVerifyForm />);

    const verifyButton = screen.getByRole("button", { name: /verify and sign in/i });
    expect(verifyButton).toBeDisabled();

    const boxes = screen.getAllByRole("textbox");
    await user.type(boxes[0]!, "4");
    await user.paste("83921");

    expect(verifyButton).not.toBeDisabled();
  });

  it("submits the code and redirects home on success", async () => {
    const user = userEvent.setup();
    renderWithIntl(<CodeVerifyForm />);

    const boxes = screen.getAllByRole("textbox");
    await user.click(boxes[0]!);
    await user.paste("482931");
    await user.click(screen.getByRole("button", { name: /verify and sign in/i }));

    expect(verifyOtpMutate).toHaveBeenCalledWith(
      { data: { identifier: "a.adeyemi@example.org", code: "482931" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    verifyOtpMutate.mock.calls[0][1].onSuccess({ id: "1", name: "A", email: "a.adeyemi@example.org" });
    expect(push).toHaveBeenCalledWith("/home");
  });

  it("shows a resend link once the cooldown clears and requests a new code", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ delay: null });
    renderWithIntl(<CodeVerifyForm />);

    await vi.advanceTimersByTimeAsync(60_000);

    const resendButton = screen.getByRole("button", { name: /^resend$/i });
    await user.click(resendButton);

    expect(requestOtpMutate).toHaveBeenCalledWith(
      { data: { identifier: "a.adeyemi@example.org" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );

    vi.useRealTimers();
  });
});
