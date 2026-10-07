"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import type { ApiError } from "@/lib/api/client";
import type { MeResponse, MfaChallengeResponse } from "@/lib/api/generated/hRSApiV1.schemas";
import { useLogin, useRequestOtp } from "@/lib/api/generated/identity/identity";
import {
  codeLoginSchema,
  passwordLoginSchema,
  type CodeLoginFormValues,
  type PasswordLoginFormValues,
} from "@/features/identity/schemas/login";
import { cn } from "@/lib/utils";

function isMfaChallenge(result: MeResponse | MfaChallengeResponse): result is MfaChallengeResponse {
  return "challengeToken" in result;
}

export function LoginForm() {
  const t = useTranslations("Login");
  const router = useRouter();
  const [mode, setMode] = useState<"password" | "code">("password");

  const login = useLogin<ApiError>();
  const requestOtp = useRequestOtp<ApiError>();

  const passwordForm = useForm<PasswordLoginFormValues>({
    resolver: zodResolver(passwordLoginSchema),
    defaultValues: { identifier: "", password: "" },
  });
  const codeForm = useForm<CodeLoginFormValues>({
    resolver: zodResolver(codeLoginSchema),
    defaultValues: { identifier: "" },
  });

  const handleOutcome = (result: MeResponse | MfaChallengeResponse) => {
    if (isMfaChallenge(result)) {
      router.push(`/login/mfa?challenge=${encodeURIComponent(result.challengeToken)}`);
    } else {
      router.push("/home");
    }
  };

  const onPasswordSubmit = (values: PasswordLoginFormValues) => {
    login.mutate({ data: values }, { onSuccess: handleOutcome });
  };

  const onCodeSubmit = (values: CodeLoginFormValues) => {
    requestOtp.mutate(
      { data: { identifier: values.identifier } },
      {
        onSuccess: () => router.push(`/login/code?identifier=${encodeURIComponent(values.identifier)}`),
      },
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle")}</p>
      </div>

      <div className="flex rounded-md bg-ground p-1" role="tablist" aria-label={t("methodLabel")}>
        <TabButton active={mode === "password"} onClick={() => setMode("password")} label={t("tabPassword")} />
        <TabButton active={mode === "code"} onClick={() => setMode("code")} label={t("tabCode")} />
      </div>

      {mode === "password" ? (
        <form onSubmit={passwordForm.handleSubmit(onPasswordSubmit)} className="flex flex-col gap-4" noValidate>
          <Field id="login-email" label={t("emailLabel")} error={passwordForm.formState.errors.identifier?.message}>
            <Input id="login-email" autoComplete="username" {...passwordForm.register("identifier")} />
          </Field>
          <Field id="login-password" label={t("passwordLabel")} error={passwordForm.formState.errors.password?.message}>
            <PasswordInput
              id="login-password"
              autoComplete="current-password"
              showPasswordLabel={t("showPassword")}
              hidePasswordLabel={t("hidePassword")}
              {...passwordForm.register("password")}
            />
          </Field>

          <div className="flex items-center justify-end text-sm">
            <Link href="/login/forgot" className="font-medium text-green-700 hover:underline">
              {t("forgotPassword")}
            </Link>
          </div>

          {login.isError ? <ErrorBanner message={login.error.message} /> : null}

          <Button type="submit" disabled={login.isPending}>
            {login.isPending ? t("signingIn") : t("signIn")}
          </Button>
        </form>
      ) : (
        <form onSubmit={codeForm.handleSubmit(onCodeSubmit)} className="flex flex-col gap-4" noValidate>
          <Field id="login-code-email" label={t("emailLabel")} error={codeForm.formState.errors.identifier?.message}>
            <Input id="login-code-email" autoComplete="username" {...codeForm.register("identifier")} />
          </Field>

          {requestOtp.isError ? <ErrorBanner message={requestOtp.error.message} /> : null}

          <Button type="submit" disabled={requestOtp.isPending}>
            {requestOtp.isPending ? t("sendingCode") : t("sendCode")}
          </Button>
        </form>
      )}

      <p className="text-center text-xs text-ink-muted">{t("inviteNotice")}</p>
    </div>
  );
}

function TabButton({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "min-h-9 flex-1 rounded-sm text-sm font-medium transition-colors",
        active ? "bg-surface text-ink shadow-xs" : "text-ink-muted",
      )}
    >
      {label}
    </button>
  );
}

export function Field({ id, label, error, children }: { id: string; label: string; error?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? <p className="text-xs text-danger">{error}</p> : null}
    </div>
  );
}

export function ErrorBanner({ message }: { message: string }) {
  return (
    <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
      {message}
    </p>
  );
}
