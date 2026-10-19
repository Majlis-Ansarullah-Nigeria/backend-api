import { CompareScreen } from "@/features/history/components/CompareScreen";

export default async function ComparePage({
  searchParams,
}: {
  searchParams: Promise<{ unitId?: string; reportTypeId?: string; periodA?: string; periodB?: string }>;
}) {
  const { unitId, reportTypeId, periodA, periodB } = await searchParams;

  return <CompareScreen initial={{ unitId, reportTypeId, periodA, periodB }} />;
}
