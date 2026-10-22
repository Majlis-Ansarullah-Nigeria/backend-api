import { IndicatorsScreen } from "@/features/dashboards/components/IndicatorsScreen";
import { parseIndicatorSearchParams, type IndicatorSearchParams } from "@/features/dashboards/utils/search-params";

/** Zone/Dila heads' scoped dashboard (FR-DSH-005) and Leadership's dashboard. */
export default async function DashboardsPage({ searchParams }: { searchParams: Promise<IndicatorSearchParams> }) {
  return <IndicatorsScreen basePath="/dashboards" initial={parseIndicatorSearchParams(await searchParams)} />;
}
