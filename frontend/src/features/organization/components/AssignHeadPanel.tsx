"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import { getGetOrganizationTreeQueryKey, useCreateRoleAssignment } from "@/lib/api/generated/organization/organization";
import { ROLE_NAME } from "@/features/admin/utils/role-name";
import { VacantHeadBadge } from "./badges";

const assignHeadSchema = z.object({
  email: z.string().trim().email("Enter a valid email address"),
  name: z.string().trim().min(1, "Enter a name"),
  startsAt: z.string().min(1, "Choose a start date"),
  endsAt: z.string().optional().or(z.literal("")),
});

type AssignHeadFormValues = z.infer<typeof assignHeadSchema>;

function toIsoMidnightUtc(dateOnly: string): string {
  return new Date(`${dateOnly}T00:00:00Z`).toISOString();
}

export function AssignHeadPanel({
  unitId,
  unitName,
  headName,
  hasVacantHead,
}: {
  unitId: string;
  unitName: string;
  headName: string | null;
  hasVacantHead: boolean;
}) {
  const t = useTranslations("Organization");
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [result, setResult] = useState<"assigned" | "invited" | null>(null);
  const createAssignment = useCreateRoleAssignment<ApiError>();

  const form = useForm<AssignHeadFormValues>({
    resolver: zodResolver(assignHeadSchema),
    defaultValues: { email: "", name: "", startsAt: new Date().toISOString().slice(0, 10), endsAt: "" },
  });

  const onSubmit = (values: AssignHeadFormValues) => {
    createAssignment.mutate(
      {
        id: unitId,
        data: {
          email: values.email,
          name: values.name,
          role: ROLE_NAME.UnitHead,
          delegateScope: null,
          startsAt: toIsoMidnightUtc(values.startsAt),
          endsAt: values.endsAt ? toIsoMidnightUtc(values.endsAt) : null,
        },
      },
      {
        onSuccess: (response) => {
          setResult(response.invited ? "invited" : "assigned");
          setIsOpen(false);
          form.reset();
          queryClient.invalidateQueries({ queryKey: getGetOrganizationTreeQueryKey() });
        },
      },
    );
  };

  return (
    <div className="flex flex-col gap-3 border-t border-line pt-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("headLabel")}</p>
          {hasVacantHead ? (
            <div className="mt-1">
              <VacantHeadBadge label={t("vacantHead")} />
            </div>
          ) : (
            <p className="mt-1 text-sm font-medium text-ink">{headName}</p>
          )}
        </div>
        {!isOpen ? (
          <Button type="button" variant="outline" size="sm" onClick={() => setIsOpen(true)}>
            {hasVacantHead ? t("assignHead") : t("changeHead")}
          </Button>
        ) : null}
      </div>

      {result ? (
        <p className="rounded-md bg-status-acknowledged-bg px-3 py-2 text-sm text-status-acknowledged-fg">
          {result === "invited" ? t("headInvited") : t("headAssigned")}
        </p>
      ) : null}

      {isOpen ? (
        <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-3 rounded-md bg-ground p-3" noValidate>
          <p className="text-sm font-medium text-ink">{t("assignHeadTo", { unit: unitName })}</p>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="head-email">{t("emailLabel")}</Label>
            <Input id="head-email" {...form.register("email")} aria-invalid={!!form.formState.errors.email} />
            {form.formState.errors.email ? <p className="text-xs text-danger">{form.formState.errors.email.message}</p> : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="head-name">{t("headNameLabel")}</Label>
            <Input id="head-name" {...form.register("name")} aria-invalid={!!form.formState.errors.name} />
            {form.formState.errors.name ? <p className="text-xs text-danger">{form.formState.errors.name.message}</p> : null}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="head-starts">{t("termStartLabel")}</Label>
              <Input id="head-starts" type="date" {...form.register("startsAt")} />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="head-ends">{t("termEndLabel")}</Label>
              <Input id="head-ends" type="date" {...form.register("endsAt")} />
            </div>
          </div>

          {createAssignment.isError ? (
            <p role="alert" className="rounded-sm bg-status-returned-bg px-3 py-2 text-sm text-status-returned-fg">
              {createAssignment.error.message}
            </p>
          ) : null}

          <div className="flex gap-2">
            <Button type="submit" size="sm" disabled={createAssignment.isPending}>
              {createAssignment.isPending ? t("assigning") : t("assignAndInvite")}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setIsOpen(false)}>
              {t("cancel")}
            </Button>
          </div>
        </form>
      ) : null}
    </div>
  );
}
