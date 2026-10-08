import { UNIT_LEVEL_CODE, levelLabel } from "@/features/templates/utils/report-type";

export interface DueDateForWarnings {
  level: number;
  dueAt: string;
}

// Mirrors backend/src/HRS.Application/Features/Windows/SubmissionWindows/ParentBeforeChildWarnings.cs
// — ported so the "Create window" screen can show the warning live, before saving. The backend
// re-detects it independently and is the authoritative copy returned on save.
const ROLL_UP_CHAIN = [UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.National];

export function detectParentBeforeChildWarnings(dueDates: DueDateForWarnings[]): string[] {
  const byLevel = new Map(dueDates.map((d) => [d.level, d.dueAt]));
  const warnings: string[] = [];

  for (let i = 0; i < ROLL_UP_CHAIN.length - 1; i++) {
    const childLevel = ROLL_UP_CHAIN[i];
    const parentLevel = ROLL_UP_CHAIN[i + 1];
    const childDueAt = byLevel.get(childLevel);
    const parentDueAt = byLevel.get(parentLevel);

    if (!childDueAt || !parentDueAt) {
      continue;
    }

    if (new Date(parentDueAt) < new Date(childDueAt)) {
      warnings.push(
        `${levelLabel(parentLevel)} is due before ${levelLabel(childLevel)} — ${levelLabel(parentLevel)} heads won't be able to pull complete ${levelLabel(childLevel)} figures.`,
      );
    }
  }

  return warnings;
}
