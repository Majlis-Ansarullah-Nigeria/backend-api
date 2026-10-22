import type { IndicatorSelection } from "@/features/dashboards/components/IndicatorsScreen";

export interface IndicatorSearchParams {
  reportTypeId?: string;
  fieldKey?: string;
  unitId?: string;
  periods?: string;
}

export function parseIndicatorSearchParams(params: IndicatorSearchParams): IndicatorSelection {
  const periods = params.periods == null || params.periods === "" ? undefined : Number(params.periods);
  return {
    reportTypeId: params.reportTypeId || undefined,
    fieldKey: params.fieldKey || undefined,
    unitId: params.unitId || undefined,
    periods: periods != null && Number.isFinite(periods) ? periods : undefined,
  };
}
