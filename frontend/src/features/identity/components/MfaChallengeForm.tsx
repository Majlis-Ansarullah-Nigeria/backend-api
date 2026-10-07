"use client";

import { ShieldCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import { useVerifyMfa } from "@/lib/api/generated/identity/identity";
import { ErrorBanner } from "./LoginForm";

export function MfaChallengeForm() {
  const t = useTranslations("Login");
  const router = useRouter();
  const searchParams = useSearchParams();
  const challengeToken = searchParams.get("challenge") ?? "";

  const [code, setCode] = useState("");
  const verifyMfa = useVerifyMfa<ApiError>();

  const handleVerify = () => {
    verifyMfa.mutate({ data: { challengeToken, code } }, { onSuccess: () => router.push("/home") });
  };

  return (
    <div className="flex flex-col gap-4">
      <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
        <ShieldCheck className="size-6" aria-hidden="true" />
      </span>

      <div>
        <h1 className="text-2xl font-bold text-ink">{t("mfaChallengeTitle")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("mfaChallengeDescription")}</p>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="mfa-code">{t("codeLabel")}</Label>
        <Input
          id="mfa-code"
          autoComplete="one-time-code"
          value={code}
          onChange={(event) => setCode(event.target.value.trim())}
          disabled={verifyMfa.isPending}
        />
      </div>

      {verifyMfa.isError ? <ErrorBanner message={verifyMfa.error.message} /> : null}

      <Button onClick={handleVerify} disabled={code.length < 6 || verifyMfa.isPending}>
        {verifyMfa.isPending ? t("verifying") : t("verifyAndSignIn")}
      </Button>

      <p className="text-center text-xs text-ink-muted">{t("recoveryCodeNotice")}</p>
    </div>
  );
}
