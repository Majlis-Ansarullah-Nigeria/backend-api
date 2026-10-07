import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { MfaChallengeForm } from "./MfaChallengeForm";

const verifyMfaMutate = vi.fn();
const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useSearchParams: () => new URLSearchParams({ challenge: "tok_123" }),
}));

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useVerifyMfa: () => ({ mutate: verifyMfaMutate, isPending: false, isError: false }),
}));

function renderWithIntl(ui: ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  verifyMfaMutate.mockReset();
  push.mockReset();
});

describe("MfaChallengeForm", () => {
  it("accepts either a 6-digit TOTP code or a longer recovery code", async () => {
    const user = userEvent.setup();
    renderWithIntl(<MfaChallengeForm />);

    await user.type(screen.getByLabelText(/6-digit code/i), "7KQ2-91LD");
    await user.click(screen.getByRole("button", { name: /verify and sign in/i }));

    expect(verifyMfaMutate).toHaveBeenCalledWith(
      { data: { challengeToken: "tok_123", code: "7KQ2-91LD" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it("redirects home on success", async () => {
    const user = userEvent.setup();
    renderWithIntl(<MfaChallengeForm />);

    await user.type(screen.getByLabelText(/6-digit code/i), "482931");
    await user.click(screen.getByRole("button", { name: /verify and sign in/i }));

    verifyMfaMutate.mock.calls[0][1].onSuccess();
    expect(push).toHaveBeenCalledWith("/home");
  });
});
