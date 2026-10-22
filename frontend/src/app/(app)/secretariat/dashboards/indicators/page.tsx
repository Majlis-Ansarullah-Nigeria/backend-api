import { IndicatorsScreen } from "@/features/dashboards/components/IndicatorsScreen";
import { parseIndicatorSearchParams, type IndicatorSearchParams } from "@/features/dashboards/utils/search-params";

export default async function SecretariatIndicatorsPage({ searchParams }: { searchParams: Promise<IndicatorSearchParams> }) {
  return <IndicatorsScreen basePath="/secretariat/dashboards/indicators" initial={parseIndicatorSearchParams(await searchParams)} />;
}
