"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Check, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { ErrorState } from "@/components/shared/ErrorState";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import {
  acceptInvitationSchema,
  passwordCriteria,
  type AcceptInvitationFormValues,
} from "@/features/identity/schemas/accept-invitation";
import type { ApiError } from "@/lib/api/client";
import { useAcceptInvitation } from "@/lib/api/generated/identity/identity";
import { useGetInvitation } from "@/lib/api/generated/invitations/invitations";
import type { InvitationPreview } from "@/lib/api/generated/hRSApiV1.schemas";
import { cn } from "@/lib/utils";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

export interface AcceptInvitationFormProps {
  token: string;
}

const INVITE_ERROR_KEYS: Record<string, { title: string; description: string }> = {
  INVITE_NOT_FOUND: { title: "invalidTitle", description: "invalidDescription" },
  INVITE_EXPIRED: { title: "expiredTitle", description: "expiredDescription" },
  INVITE_ALREADY_ACCEPTED: { title: "alreadyAcceptedTitle", description: "alreadyAcceptedDescription" },
};

export function AcceptInvitationForm({ token }: AcceptInvitationFormProps) {
  const t = useTranslations("AcceptInvitation");

  // The generated hook's TError defaults to the bare ProblemDetails schema, which doesn't
  // describe what customFetch actually throws (ApiError, with .code/.message) — override it
  // explicitly rather than widening every call site to `unknown`.
  const preview = useGetInvitation<InvitationPreview, ApiError>(token);

  if (preview.isPending) {
    return <p className="text-sm text-ink-muted">{t("loading")}</p>;
  }

  if (preview.isError) {
    const mapped = INVITE_ERROR_KEYS[preview.error.code];
    return (
      <ErrorState
        title={t(mapped?.title ?? "invalidTitle")}
        description={t(mapped?.description ?? "invalidDescription")}
      />
    );
  }

  return <InvitationAcceptFields token={token} invitation={preview.data} />;
}

function InvitationAcceptFields({ token, invitation }: { token: string; invitation: InvitationPreview }) {
  const fmt = useOrgFormat();
  const t = useTranslations("AcceptInvitation");
  const router = useRouter();
  const acceptInvitation = useAcceptInvitation<ApiError>();

  const form = useForm<AcceptInvitationFormValues>({
    resolver: zodResolver(acceptInvitationSchema),
    defaultValues: { name: invitation.name, phone: "", password: "", agreedToPrivacyNotice: false },
  });

  const password = useWatch({ control: form.control, name: "password" });
  const agreedToPrivacyNotice = useWatch({ control: form.control, name: "agreedToPrivacyNotice" });
  const criteria = passwordCriteria(password);

  // The initial name defaultValue above already covers first paint; this just keeps the field
  // in sync if the invitation preview is refetched with a different name.
  useEffect(() => {
    form.setValue("name", invitation.name);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invitation.name]);

  const initials = invitation.name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  const onSubmit = (values: AcceptInvitationFormValues) => {
    acceptInvitation.mutate(
      {
        token,
        data: {
          name: values.name,
          phone: values.phone ? values.phone : null,
          password: values.password,
          agreedToPrivacyNotice: values.agreedToPrivacyNotice,
        },
      },
      { onSuccess: () => router.push("/") },
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-3 rounded-lg bg-mint px-4 py-3">
        <Avatar>
          <AvatarFallback>{initials}</AvatarFallback>
        </Avatar>
        <div className="min-w-0">
          <p className="text-xs font-medium text-green-700">{t("welcomeBadge")}</p>
          <p className="truncate text-base font-semibold text-ink">{t("welcomeHeading")}</p>
        </div>
      </div>

      <div>
        <h1 className="text-xl font-bold text-ink">{t("title")}</h1>
        <p className="mt-1 text-sm text-ink-muted">{t("subtitle", { name: invitation.name })}</p>
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5" noValidate>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="name">{t("fullNameLabel")}</Label>
            <Input id="name" autoComplete="name" aria-invalid={!!form.formState.errors.name} {...form.register("name")} />
            {form.formState.errors.name ? (
              <p className="text-xs text-danger">{form.formState.errors.name.message}</p>
            ) : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="phone">{t("phoneLabel")}</Label>
            <Input
              id="phone"
              type="tel"
              autoComplete="tel"
              aria-invalid={!!form.formState.errors.phone}
              {...form.register("phone")}
            />
            {form.formState.errors.phone ? (
              <p className="text-xs text-danger">{form.formState.errors.phone.message}</p>
            ) : null}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="email">{t("emailLabel")}</Label>
          <Input id="email" value={invitation.email} disabled readOnly />
          <p className="text-xs text-ink-muted">{t("emailHelp")}</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="password">{t("passwordLabel")}</Label>
          <PasswordInput
            id="password"
            autoComplete="new-password"
            aria-invalid={!!form.formState.errors.password}
            showPasswordLabel={t("showPassword")}
            hidePasswordLabel={t("hidePassword")}
            {...form.register("password")}
          />
          <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs">
            <PasswordCriterion met={criteria.length} label={t("passwordCriteriaLength")} />
            <PasswordCriterion met={criteria.digit} label={t("passwordCriteriaDigit")} />
            <PasswordCriterion met={criteria.notCommon} label={t("passwordCriteriaNotCommon")} />
          </ul>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="flex items-start gap-2.5 text-sm text-ink">
            <Checkbox
              checked={agreedToPrivacyNotice}
              onCheckedChange={(checked) => form.setValue("agreedToPrivacyNotice", checked === true)}
              aria-invalid={!!form.formState.errors.agreedToPrivacyNotice}
            />
            {t("privacyNotice")}
          </label>
          {form.formState.errors.agreedToPrivacyNotice ? (
            <p className="text-xs text-danger">{form.formState.errors.agreedToPrivacyNotice.message}</p>
          ) : null}
        </div>

        {acceptInvitation.isError ? (
          <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
            {acceptInvitation.error.message}
          </p>
        ) : null}

        <Button type="submit" disabled={acceptInvitation.isPending}>
          {acceptInvitation.isPending ? t("submitting") : t("submit")}
        </Button>
      </form>

      <p className="text-center text-xs text-ink-muted">{t("expiresOn", { date: fmt.date(invitation.expiresAt) })}</p>
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


