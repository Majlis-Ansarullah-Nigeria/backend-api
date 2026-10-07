"use client";

import { ArrowLeft, Mail } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { ApiError } from "@/lib/api/client";
import type { MeResponse, MfaChallengeResponse } from "@/lib/api/generated/hRSApiV1.schemas";
import { useRequestOtp, useVerifyOtp } from "@/lib/api/generated/identity/identity";
import { maskEmail } from "@/features/identity/utils/mask-email";
import { useCountdown } from "@/features/identity/utils/use-countdown";
import { ErrorBanner } from "./LoginForm";
import { OtpDigitInput } from "./OtpDigitInput";

const RESEND_COOLDOWN_SECONDS = 60;

function isMfaChallenge(result: MeResponse | MfaChallengeResponse): result is MfaChallengeResponse {
  return "challengeToken" in result;
}

export function CodeVerifyForm() {
  const t = useTranslations("Login");
  const router = useRouter();
  const searchParams = useSearchParams();
  const identifier = searchParams.get("identifier") ?? "";

  const [code, setCode] = useState("");
  const [resendCount, setResendCount] = useState(0);

  const verifyOtp = useVerifyOtp<ApiError>();
  const requestOtp = useRequestOtp<ApiError>();

  const handleOutcome = (result: MeResponse | MfaChallengeResponse) => {
    if (isMfaChallenge(result)) {
      router.push(`/login/mfa?challenge=${encodeURIComponent(result.challengeToken)}`);
    } else {
      router.push("/home");
    }
  };

  const handleVerify = () => {
    verifyOtp.mutate({ data: { identifier, code } }, { onSuccess: handleOutcome });
  };

  const handleResend = () => {
    requestOtp.mutate({ data: { identifier } }, { onSuccess: () => setResendCount((n) => n + 1) });
  };

  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/login"
        className="flex size-9 items-center justify-center rounded-md border border-line-strong text-ink-muted hover:bg-ground"
      >
        <ArrowLeft className="size-4" aria-hidden="true" />
        <span className="sr-only">{t("back")}</span>
      </Link>

      <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
        <Mail className="size-6" aria-hidden="true" />
      </span>

      <div>
        <h1 className="text-2xl font-bold text-ink">{t("checkEmailTitle")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("checkEmailDescription", { email: maskEmail(identifier) })}</p>
      </div>

      <OtpDigitInput value={code} onChange={setCode} disabled={verifyOtp.isPending} aria-label={t("codeLabel")} />

      {verifyOtp.isError ? <ErrorBanner message={verifyOtp.error.message} /> : null}

      <Button onClick={handleVerify} disabled={code.length !== 6 || verifyOtp.isPending}>
        {verifyOtp.isPending ? t("verifying") : t("verifyAndSignIn")}
      </Button>

      <ResendTimer key={resendCount} onResend={handleResend} isPending={requestOtp.isPending} />

      <p className="text-center text-xs text-ink-muted">{t("sharedDeviceNotice")}</p>
    </div>
  );
}

function ResendTimer({ onResend, isPending }: { onResend: () => void; isPending: boolean }) {
  const t = useTranslations("Login");
  const cooldown = useCountdown(RESEND_COOLDOWN_SECONDS);
  const minutes = Math.floor(cooldown / 60);
  const seconds = cooldown % 60;

  return (
    <p className="text-center text-sm text-ink-muted">
      {cooldown > 0 ? (
        t("resendIn", { time: `${minutes}:${seconds.toString().padStart(2, "0")}` })
      ) : (
        <>
          {t("didntGetIt")}{" "}
          <button type="button" onClick={onResend} disabled={isPending} className="font-medium text-green-700 hover:underline">
            {t("resend")}
          </button>
        </>
      )}
    </p>
  );
}
