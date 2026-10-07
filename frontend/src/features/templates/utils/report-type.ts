// Matches backend/src/HRS.Domain/Templates/{ReportTypeCategory,ReportTypeFrequency}.cs and
// HRS.Domain/Organization/UnitLevelCode.cs's enum order — the API serializes all as plain numbers.
export const REPORT_TYPE_CATEGORY = {
  GeneralMonthly: 0,
  Quarterly: 1,
  Annual: 2,
  AdHoc: 3,
  Event: 4,
  Survey: 5,
} as const;

export const REPORT_TYPE_FREQUENCY = {
  Monthly: 0,
  Quarterly: 1,
  Annual: 2,
  AdHoc: 3,
} as const;

export const UNIT_LEVEL_CODE = {
  National: 0,
  Zone: 1,
  Dila: 2,
  Muqam: 3,
  Department: 4,
} as const;

const CATEGORY_LABELS: Record<number, string> = {
  [REPORT_TYPE_CATEGORY.GeneralMonthly]: "General Monthly",
  [REPORT_TYPE_CATEGORY.Quarterly]: "Quarterly",
  [REPORT_TYPE_CATEGORY.Annual]: "Annual",
  [REPORT_TYPE_CATEGORY.AdHoc]: "Ad-hoc",
  [REPORT_TYPE_CATEGORY.Event]: "Event",
  [REPORT_TYPE_CATEGORY.Survey]: "Survey",
};

const FREQUENCY_LABELS: Record<number, string> = {
  [REPORT_TYPE_FREQUENCY.Monthly]: "Monthly",
  [REPORT_TYPE_FREQUENCY.Quarterly]: "Quarterly",
  [REPORT_TYPE_FREQUENCY.Annual]: "Annual",
  [REPORT_TYPE_FREQUENCY.AdHoc]: "Ad-hoc",
};

const LEVEL_LABELS: Record<number, string> = {
  [UNIT_LEVEL_CODE.National]: "National",
  [UNIT_LEVEL_CODE.Zone]: "Zone",
  [UNIT_LEVEL_CODE.Dila]: "Dila",
  [UNIT_LEVEL_CODE.Muqam]: "Muqam",
  [UNIT_LEVEL_CODE.Department]: "Department",
};

export function categoryLabel(category: number): string {
  return CATEGORY_LABELS[category] ?? "Unknown category";
}

export function frequencyLabel(frequency: number): string {
  return FREQUENCY_LABELS[frequency] ?? "Unknown frequency";
}

export function levelLabel(level: number): string {
  return LEVEL_LABELS[level] ?? "Unknown level";
}

/** "Muqam, Dila, Zone" — in fixed hierarchy order, not whatever order the API returned. */
export function applicableLevelsLabel(levels: readonly number[]): string {
  const order = [UNIT_LEVEL_CODE.Zone, UNIT_LEVEL_CODE.Dila, UNIT_LEVEL_CODE.Muqam, UNIT_LEVEL_CODE.Department, UNIT_LEVEL_CODE.National];
  return order
    .filter((level) => levels.includes(level))
    .map(levelLabel)
    .join(", ");
}
