"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ApiError } from "@/lib/api/client";
import type { UnitTreeNode, UserListItem } from "@/lib/api/generated/hRSApiV1.schemas";
import {
  getGetUsersQueryKey,
  useAdminResetMfa,
  useDeactivateUser,
  useReactivateUser,
  useRevokeAllSessions,
} from "@/lib/api/generated/admin/admin";
import { getGetOrganizationTreeQueryKey, useCreateRoleAssignment } from "@/lib/api/generated/organization/organization";
import { DELEGATE_SCOPE, ROLE_NAME, delegateScopeLabel, roleLabel } from "@/features/admin/utils/role-name";
import { USER_STATUS } from "@/features/admin/utils/users";
import { useOrgFormat } from "@/lib/org/OrgFormatContext";

const addRoleSchema = z.object({
  unitId: z.string().trim().min(1, "Choose a unit"),
  role: z.string(),
  delegateScope: z.string().optional(),
  startsAt: z.string().min(1, "Choose a start date"),
  endsAt: z.string().optional().or(z.literal("")),
});

type AddRoleFormValues = z.infer<typeof addRoleSchema>;

function toIsoMidnightUtc(dateOnly: string): string {
  return new Date(`${dateOnly}T00:00:00Z`).toISOString();
}

export function UserDetailPanel({ user, units }: { user: UserListItem; units: UnitTreeNode[] }) {
  const fmt = useOrgFormat();
  const t = useTranslations("AdminUsers");
  const queryClient = useQueryClient();
  const [isAddingRole, setIsAddingRole] = useState(false);

  const deactivate = useDeactivateUser<ApiError>();
  const reactivate = useReactivateUser<ApiError>();
  const resetMfa = useAdminResetMfa<ApiError>();
  const revokeSessions = useRevokeAllSessions<ApiError>();

  const invalidate = () => queryClient.invalidateQueries({ queryKey: getGetUsersQueryKey() });

  const initials = user.name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-line bg-surface p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-mint text-sm font-bold text-green-700">
          {initials}
        </span>
        <div className="min-w-0">
          <p className="truncate font-semibold text-ink">{user.name}</p>
          <p className="truncate text-sm text-ink-muted">{user.email}</p>
        </div>
      </div>

      <div>
        <div className="flex items-center justify-between">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("roleAssignments")}</p>
          {user.userId ? (
            <button type="button" onClick={() => setIsAddingRole((v) => !v)} className="text-xs font-medium text-green-700 hover:underline">
              + {t("addRole")}
            </button>
          ) : null}
        </div>

        {user.roleAssignments.length === 0 ? (
          <p className="mt-2 text-sm text-ink-muted">{t("noRoleAssignments")}</p>
        ) : (
          <ul className="mt-2 flex flex-col gap-2">
            {user.roleAssignments.map((assignment) => (
              <li key={assignment.id} className="rounded-md bg-ground p-2.5">
                <p className="text-sm font-medium text-ink">
                  {roleLabel(assignment.role)}
                  {assignment.delegateScope !== null ? ` · ${delegateScopeLabel(assignment.delegateScope)}` : ""} · {assignment.unitName}
                </p>
                <p className="text-xs text-ink-muted">
                  {fmt.date(assignment.startsAt)} –{" "}
                  {assignment.endsAt ? fmt.date(assignment.endsAt) : t("openEnded")}
                  {!assignment.isActive ? ` · ${t("notYetActive")}` : ""}
                </p>
              </li>
            ))}
          </ul>
        )}

        {isAddingRole && user.userId ? (
          <AddRoleForm
            email={user.email}
            name={user.name}
            units={units}
            onDone={() => {
              setIsAddingRole(false);
              invalidate();
              queryClient.invalidateQueries({ queryKey: getGetOrganizationTreeQueryKey() });
            }}
          />
        ) : null}
      </div>

      {user.userId ? (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{t("account")}</p>
          <div className="mt-2 flex flex-col gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={resetMfa.isPending}
              onClick={() => resetMfa.mutate({ id: user.userId! }, { onSuccess: invalidate })}
            >
              {resetMfa.isPending ? t("resettingMfa") : t("resetMfa")}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={revokeSessions.isPending}
              onClick={() => revokeSessions.mutate({ id: user.userId! })}
            >
              {revokeSessions.isPending ? t("signingOut") : t("signOutEverywhere")}
            </Button>
            {user.status === USER_STATUS.Deactivated ? (
              <Button
                size="sm"
                disabled={reactivate.isPending}
                onClick={() => reactivate.mutate({ id: user.userId! }, { onSuccess: invalidate })}
              >
                {reactivate.isPending ? t("reactivating") : t("reactivate")}
              </Button>
            ) : (
              <Button
                variant="destructive"
                size="sm"
                disabled={deactivate.isPending}
                onClick={() => deactivate.mutate({ id: user.userId! }, { onSuccess: invalidate })}
              >
                {deactivate.isPending ? t("deactivating") : t("deactivate")}
              </Button>
            )}
            {deactivate.isError ? <p className="text-xs text-danger">{deactivate.error.message}</p> : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function AddRoleForm({
  email,
  name,
  units,
  onDone,
}: {
  email: string;
  name: string;
  units: UnitTreeNode[];
  onDone: () => void;
}) {
  const t = useTranslations("AdminUsers");
  const createAssignment = useCreateRoleAssignment<ApiError>();

  const form = useForm<AddRoleFormValues>({
    resolver: zodResolver(addRoleSchema),
    defaultValues: {
      unitId: "",
      role: String(ROLE_NAME.UnitHead),
      delegateScope: "",
      startsAt: new Date().toISOString().slice(0, 10),
      endsAt: "",
    },
  });

  const role = useWatch({ control: form.control, name: "role" });
  const nonArchivedUnits = units.filter((u) => u.archivedAt === null).slice().sort((a, b) => a.path.localeCompare(b.path));

  const onSubmit = (values: AddRoleFormValues) => {
    createAssignment.mutate(
      {
        id: values.unitId,
        data: {
          email,
          name,
          role: Number(values.role),
          delegateScope: Number(values.role) === ROLE_NAME.Delegate ? Number(values.delegateScope) : null,
          startsAt: toIsoMidnightUtc(values.startsAt),
          endsAt: values.endsAt ? toIsoMidnightUtc(values.endsAt) : null,
        },
      },
      { onSuccess: onDone },
    );
  };

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="mt-3 flex flex-col gap-3 rounded-md bg-ground p-3" noValidate>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="role-unit">{t("unitLabel")}</Label>
        <select
          id="role-unit"
          {...form.register("unitId")}
          className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          <option value="">{t("selectPlaceholder")}</option>
          {nonArchivedUnits.map((unit) => (
            <option key={unit.id} value={unit.id}>
              {unit.displayCode} — {unit.name}
            </option>
          ))}
        </select>
        {form.formState.errors.unitId ? <p className="text-xs text-danger">{form.formState.errors.unitId.message}</p> : null}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="role-role">{t("roleLabel")}</Label>
        <select
          id="role-role"
          {...form.register("role")}
          className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
        >
          <option value={ROLE_NAME.UnitHead}>{t("roleUnitHead")}</option>
          <option value={ROLE_NAME.Delegate}>{t("roleDelegate")}</option>
          <option value={ROLE_NAME.Secretariat}>{t("roleSecretariat")}</option>
          <option value={ROLE_NAME.Leadership}>{t("roleLeadership")}</option>
          <option value={ROLE_NAME.DeptOfficer}>{t("roleDeptOfficer")}</option>
          <option value={ROLE_NAME.SysAdmin}>{t("roleSysAdmin")}</option>
        </select>
      </div>

      {Number(role) === ROLE_NAME.Delegate ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="role-scope">{t("delegateScopeLabel")}</Label>
          <select
            id="role-scope"
            {...form.register("delegateScope")}
            className="h-11 w-full rounded-sm border border-line-strong bg-surface px-3 text-base text-ink outline-none focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40"
          >
            <option value="">{t("selectPlaceholder")}</option>
            <option value={DELEGATE_SCOPE.DraftOnly}>{t("scopeDraftOnly")}</option>
            <option value={DELEGATE_SCOPE.DraftAndSubmit}>{t("scopeDraftAndSubmit")}</option>
          </select>
        </div>
      ) : null}

      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="role-starts">{t("termStartLabel")}</Label>
          <Input id="role-starts" type="date" {...form.register("startsAt")} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="role-ends">{t("termEndLabel")}</Label>
          <Input id="role-ends" type="date" {...form.register("endsAt")} />
        </div>
      </div>

      {createAssignment.isError ? <p className="text-xs text-danger">{createAssignment.error.message}</p> : null}

      <Button type="submit" size="sm" disabled={createAssignment.isPending}>
        {createAssignment.isPending ? t("adding") : t("addRole")}
      </Button>
    </form>
  );
}
