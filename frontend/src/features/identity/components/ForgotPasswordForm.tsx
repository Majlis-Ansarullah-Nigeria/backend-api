"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Mail } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ApiError } from "@/lib/api/client";
import { useForgotPassword } from "@/lib/api/generated/identity/identity";
import { forgotPasswordSchema, type ForgotPasswordFormValues } from "@/features/identity/schemas/login";
import { ErrorBanner, Field } from "./LoginForm";

export function ForgotPasswordForm() {
  const t = useTranslations("Login");
  const [submittedTo, setSubmittedTo] = useState<string | null>(null);
  const forgotPassword = useForgotPassword<ApiError>();

  const form = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { identifier: "" },
  });

  const onSubmit = (values: ForgotPasswordFormValues) => {
    forgotPassword.mutate(
      { data: { identifier: values.identifier } },
      { onSuccess: () => setSubmittedTo(values.identifier) },
    );
  };

  if (submittedTo) {
    return (
      <div className="flex flex-col items-center gap-4 text-center">
        <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
          <Mail className="size-6" aria-hidden="true" />
        </span>
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("forgotCheckEmailTitle")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("forgotCheckEmailDescription", { identifier: submittedTo })}</p>
        </div>
        <Link href="/login" className="text-sm font-medium text-green-700 hover:underline">
          {t("backToSignIn")}
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("forgotTitle")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("forgotSubtitle")}</p>
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        <Field id="forgot-identifier" label={t("emailLabel")} error={form.formState.errors.identifier?.message}>
          <Input id="forgot-identifier" autoComplete="username" {...form.register("identifier")} />
        </Field>

        {forgotPassword.isError ? <ErrorBanner message={forgotPassword.error.message} /> : null}

        <Button type="submit" disabled={forgotPassword.isPending}>
          {forgotPassword.isPending ? t("sendingResetLink") : t("sendResetLink")}
        </Button>
      </form>

      <p className="text-center text-sm text-ink-muted">
        <Link href="/login" className="font-medium text-green-700 hover:underline">
          {t("backToSignIn")}
        </Link>
      </p>
    </div>
  );
}
