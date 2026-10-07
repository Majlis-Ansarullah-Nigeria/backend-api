"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Check, Copy, Download, ShieldCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ErrorState } from "@/components/shared/ErrorState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ApiError } from "@/lib/api/client";
import type { MeProfile } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetMeQueryKey,
  useBeginTotpEnrollment,
  useConfirmTotpEnrollment,
  useDisableTotp,
  useGetMe,
} from "@/lib/api/generated/identity/identity";
import { useQueryClient } from "@tanstack/react-query";
import { Field, ErrorBanner } from "@/features/identity/components/LoginForm";

const confirmSchema = z.object({ code: z.string().trim().min(6, "Enter the 6-digit code.") });
type ConfirmFormValues = z.infer<typeof confirmSchema>;

export function MfaSetupScreen() {
  const t = useTranslations("Mfa");
  const me = useGetMe<MeProfile, ApiError>();

  if (me.isPending) {
    return <p className="p-6 text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (me.isError) {
    return (
      <div className="p-6">
        <ErrorState title={t("loadErrorTitle")} onRetry={() => me.refetch()} />
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-xl flex-col gap-6 p-4 sm:p-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      {me.data.mfaEnabled ? (
        <EnabledState canDisable={!me.data.mfaRequiredByRole} />
      ) : (
        <EnrollmentFlow mandatory={me.data.mfaRequiredByRole} />
      )}
    </div>
  );
}

function EnabledState({ canDisable }: { canDisable: boolean }) {
  const t = useTranslations("Mfa");
  const queryClient = useQueryClient();
  const disableTotp = useDisableTotp<ApiError>();

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-mint text-green-600">
          <ShieldCheck className="size-5" aria-hidden="true" />
        </span>
        <div>
          <p className="font-semibold text-ink">{t("enabledTitle")}</p>
          <p className="text-sm text-ink-muted">{t("enabledDescription")}</p>
        </div>
      </div>

      {disableTotp.isError ? <ErrorBanner message={disableTotp.error.message} /> : null}

      {canDisable ? (
        <Button
          variant="destructive"
          className="self-start"
          disabled={disableTotp.isPending}
          onClick={() =>
            disableTotp.mutate(undefined, {
              onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
            })
          }
        >
          {disableTotp.isPending ? t("disabling") : t("disable")}
        </Button>
      ) : (
        <p className="text-sm text-ink-muted">{t("mandatoryNotice")}</p>
      )}
    </div>
  );
}

function EnrollmentFlow({ mandatory }: { mandatory: boolean }) {
  const t = useTranslations("Mfa");
  const queryClient = useQueryClient();
  const beginEnrollment = useBeginTotpEnrollment<ApiError>();
  const confirmEnrollment = useConfirmTotpEnrollment<ApiError>();
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);

  const form = useForm<ConfirmFormValues>({ resolver: zodResolver(confirmSchema), defaultValues: { code: "" } });

  useEffect(() => {
    beginEnrollment.mutate(undefined, {
      onSuccess: async (enrollment) => {
        const QRCode = (await import("qrcode")).default;
        setQrDataUrl(await QRCode.toDataURL(enrollment.authenticatorUri));
      },
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onSubmit = (values: ConfirmFormValues) => {
    confirmEnrollment.mutate(
      { data: { code: values.code } },
      {
        onSuccess: (result) => {
          setRecoveryCodes(result.recoveryCodes);
          queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
        },
      },
    );
  };

  if (recoveryCodes) {
    return <RecoveryCodesDone codes={recoveryCodes} />;
  }

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      {mandatory ? <p className="text-sm text-ink-muted">{t("mandatoryForRole")}</p> : null}

      {beginEnrollment.isError ? (
        <ErrorBanner message={beginEnrollment.error.message} />
      ) : beginEnrollment.isPending || !qrDataUrl || !beginEnrollment.data ? (
        <p className="text-sm text-ink-muted">{t("preparing")}</p>
      ) : (
        <>
          <div className="flex flex-col items-center gap-3 sm:flex-row sm:items-start">
            {/* eslint-disable-next-line @next/next/no-img-element -- a client-generated data: URL, not an optimizable remote asset */}
            <img src={qrDataUrl} alt={t("qrAlt")} className="size-40 shrink-0 rounded-md border border-line" />
            <div className="min-w-0">
              <p className="text-sm font-medium text-ink">{t("scanInstructions")}</p>
              <p className="mt-2 text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("manualKeyLabel")}</p>
              <p className="break-all font-mono text-sm text-ink">{beginEnrollment.data.sharedKey}</p>
            </div>
          </div>

          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-3" noValidate>
            <Field id="totp-code" label={t("codeLabel")} error={form.formState.errors.code?.message}>
              <Input id="totp-code" inputMode="numeric" autoComplete="one-time-code" {...form.register("code")} />
            </Field>

            {confirmEnrollment.isError ? <ErrorBanner message={confirmEnrollment.error.message} /> : null}

            <Button type="submit" disabled={confirmEnrollment.isPending} className="self-start">
              {confirmEnrollment.isPending ? t("confirming") : t("confirm")}
            </Button>
          </form>
        </>
      )}
    </div>
  );
}

function RecoveryCodesDone({ codes }: { codes: string[] }) {
  const t = useTranslations("Mfa");

  const handleCopy = async () => {
    await navigator.clipboard.writeText(codes.join("\n"));
  };

  const handleDownload = () => {
    const blob = new Blob([codes.join("\n")], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "hrs-recovery-codes.txt";
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-mint text-green-600">
          <Check className="size-5" aria-hidden="true" />
        </span>
        <div>
          <p className="font-semibold text-ink">{t("doneTitle")}</p>
          <p className="text-sm text-ink-muted">{t("recoveryCodesDescription")}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {codes.map((code) => (
          <code key={code} className="rounded-md bg-ground px-2 py-1.5 text-center font-mono text-sm text-ink">
            {code}
          </code>
        ))}
      </div>

      <div className="flex gap-2">
        <Button variant="outline" onClick={handleCopy}>
          <Copy aria-hidden="true" />
          {t("copy")}
        </Button>
        <Button variant="outline" onClick={handleDownload}>
          <Download aria-hidden="true" />
          {t("download")}
        </Button>
      </div>
    </div>
  );
}
