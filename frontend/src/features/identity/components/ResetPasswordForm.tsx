"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Check, X } from "lucide-react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { PasswordInput } from "@/components/ui/password-input";
import type { ApiError } from "@/lib/api/client";
import { useResetPassword } from "@/lib/api/generated/identity/identity";
import { PASSWORD_MIN_LENGTH, passwordCriteria } from "@/features/identity/schemas/accept-invitation";
import { cn } from "@/lib/utils";
import { ErrorBanner, Field } from "./LoginForm";

const resetPasswordSchema = z.object({
  password: z.string().min(PASSWORD_MIN_LENGTH).regex(/\d/),
});

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export function ResetPasswordForm({ token }: { token: string }) {
  const t = useTranslations("Login");
  const router = useRouter();
  const searchParams = useSearchParams();
  const email = searchParams.get("email") ?? "";

  const [isDone, setIsDone] = useState(false);
  const resetPassword = useResetPassword<ApiError>();

  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: "" },
  });

  const password = useWatch({ control: form.control, name: "password" });
  const criteria = passwordCriteria(password ?? "");

  const onSubmit = (values: ResetPasswordFormValues) => {
    resetPassword.mutate(
      { data: { email, token, newPassword: values.password } },
      { onSuccess: () => setIsDone(true) },
    );
  };

  if (isDone) {
    return (
      <div className="flex flex-col items-center gap-4 text-center">
        <span className="flex size-14 items-center justify-center rounded-full bg-mint text-green-600">
          <Check className="size-6" aria-hidden="true" />
        </span>
        <div>
          <h1 className="text-2xl font-bold text-ink">{t("resetDoneTitle")}</h1>
          <p className="mt-1 text-sm text-ink-muted">{t("resetDoneDescription")}</p>
        </div>
        <Button onClick={() => router.push("/login")}>{t("backToSignIn")}</Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t("resetTitle")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("resetSubtitle")}</p>
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
        <Field id="reset-password" label={t("newPasswordLabel")} error={form.formState.errors.password?.message}>
          <PasswordInput
            id="reset-password"
            autoComplete="new-password"
            showPasswordLabel={t("showPassword")}
            hidePasswordLabel={t("hidePassword")}
            {...form.register("password")}
          />
          <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs">
            <PasswordCriterion met={criteria.length} label={t("passwordCriteriaLength")} />
            <PasswordCriterion met={criteria.digit} label={t("passwordCriteriaDigit")} />
            <PasswordCriterion met={criteria.notCommon} label={t("passwordCriteriaNotCommon")} />
          </ul>
        </Field>

        {resetPassword.isError ? <ErrorBanner message={resetPassword.error.message} /> : null}

        <Button type="submit" disabled={resetPassword.isPending}>
          {resetPassword.isPending ? t("resettingPassword") : t("resetPassword")}
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

function PasswordCriterion({ met, label }: { met: boolean; label: string }) {
  const Icon = met ? Check : X;
  return (
    <li className={cn("flex items-center gap-1", met ? "text-green-700" : "text-ink-muted")}>
      <Icon className="size-3.5" aria-hidden="true" />
      {label}
    </li>
  );
}
