import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../../../messages/en.json";
import { MfaSetupScreen } from "./MfaSetupScreen";

const useGetMe = vi.fn();
const beginEnrollmentMutate = vi.fn();
const confirmEnrollmentMutate = vi.fn();
const disableTotpMutate = vi.fn();

vi.mock("@/lib/api/generated/identity/identity", () => ({
  useGetMe: (...args: unknown[]) => useGetMe(...args),
  useBeginTotpEnrollment: () => ({ mutate: beginEnrollmentMutate, isPending: false, isError: false, data: ENROLLMENT }),
  useConfirmTotpEnrollment: () => ({ mutate: confirmEnrollmentMutate, isPending: false, isError: false }),
  useDisableTotp: () => ({ mutate: disableTotpMutate, isPending: false, isError: false }),
  getGetMeQueryKey: () => ["getMe"],
}));

vi.mock("qrcode", () => ({
  default: { toDataURL: vi.fn().mockResolvedValue("data:image/png;base64,FAKE") },
}));

const ENROLLMENT = { sharedKey: "ABCD1234WXYZ", authenticatorUri: "otpauth://totp/HRS:a@example.org?secret=ABCD1234WXYZ" };

function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient();
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  useGetMe.mockReset();
  beginEnrollmentMutate.mockReset();
  confirmEnrollmentMutate.mockReset();
  disableTotpMutate.mockReset();
  beginEnrollmentMutate.mockImplementation((_data: unknown, options?: { onSuccess?: (e: typeof ENROLLMENT) => void }) => {
    options?.onSuccess?.(ENROLLMENT);
  });
});

describe("MfaSetupScreen", () => {
  it("shows a loading message while the profile loads", () => {
    useGetMe.mockReturnValue({ isPending: true, isError: false, data: undefined });

    renderWithProviders(<MfaSetupScreen />);

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("starts enrollment and shows the QR code and manual key", async () => {
    useGetMe.mockReturnValue({ isPending: false, isError: false, data: { mfaEnabled: false, mfaRequiredByRole: true } });

    renderWithProviders(<MfaSetupScreen />);

    await waitFor(() => expect(screen.getByAltText(/qr code/i)).toBeInTheDocument());
    expect(screen.getByText("ABCD1234WXYZ")).toBeInTheDocument();
  });

  it("confirms the code and shows recovery codes", async () => {
    useGetMe.mockReturnValue({ isPending: false, isError: false, data: { mfaEnabled: false, mfaRequiredByRole: false } });
    confirmEnrollmentMutate.mockImplementation(
      (_data: unknown, options?: { onSuccess?: (r: { recoveryCodes: string[] }) => void }) => {
        options?.onSuccess?.({ recoveryCodes: ["AAAA-1111", "BBBB-2222"] });
      },
    );

    const user = userEvent.setup();
    renderWithProviders(<MfaSetupScreen />);

    await waitFor(() => screen.getByLabelText(/6-digit code/i));
    await user.type(screen.getByLabelText(/6-digit code/i), "482931");
    await user.click(screen.getByRole("button", { name: /^confirm$/i }));

    expect(confirmEnrollmentMutate).toHaveBeenCalledWith(
      { data: { code: "482931" } },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
    expect(screen.getByText("AAAA-1111")).toBeInTheDocument();
    expect(screen.getByText("BBBB-2222")).toBeInTheDocument();
  });

  it("shows a 'turn off' button when MFA is on and not mandatory for the role", () => {
    useGetMe.mockReturnValue({ isPending: false, isError: false, data: { mfaEnabled: true, mfaRequiredByRole: false } });

    renderWithProviders(<MfaSetupScreen />);

    expect(screen.getByRole("button", { name: /turn off/i })).toBeInTheDocument();
  });

  it("hides the 'turn off' button when MFA is mandatory for the role", () => {
    useGetMe.mockReturnValue({ isPending: false, isError: false, data: { mfaEnabled: true, mfaRequiredByRole: true } });

    renderWithProviders(<MfaSetupScreen />);

    expect(screen.queryByRole("button", { name: /turn off/i })).not.toBeInTheDocument();
    expect(screen.getByText(/can't be turned off/i)).toBeInTheDocument();
  });
});
