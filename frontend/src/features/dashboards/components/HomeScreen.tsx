"use client";

import { ApiError } from "@/lib/api/client";
import type { MeProfile } from "@/lib/api/generated/hRSApiV1.schemas";
import { useGetMe } from "@/lib/api/generated/identity/identity";
import { ROLE_NAME } from "@/features/admin/utils/role-name";
import { LeadershipOverviewScreen } from "@/features/dashboards/components/LeadershipOverviewScreen";
import { HeadHomeScreen } from "@/features/submissions/components/HeadHomeScreen";

/** Leadership's home is their national overview when acting as Leadership — the active
 * assignment, or their only kind of assignment. Everyone else gets the head home. Navigation
 * only: the overview's own API still checks the role. */
export function isLeadershipHome(me: MeProfile | undefined): boolean {
  if (!me || me.roleAssignments.length === 0) {
    return false;
  }
  const active = me.roleAssignments.find((assignment) => assignment.id === me.activeRoleAssignmentId);
  if (active) {
    return Number(active.role) === ROLE_NAME.Leadership;
  }
  return me.roleAssignments.every((assignment) => Number(assignment.role) === ROLE_NAME.Leadership);
}

export function HomeScreen() {
  const me = useGetMe<MeProfile, ApiError>();
  if (me.isPending) {
    return null;
  }
  return isLeadershipHome(me.data) ? <LeadershipOverviewScreen /> : <HeadHomeScreen />;
}
