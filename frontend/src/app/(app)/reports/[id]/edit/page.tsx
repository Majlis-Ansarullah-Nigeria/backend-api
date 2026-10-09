import { ReportFormScreen } from "@/features/submissions/components/ReportFormScreen";

export default async function ReportFormPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ unit?: string }>;
}) {
  const { id } = await params;
  const { unit } = await searchParams;

  return <ReportFormScreen id={id} unitId={unit ?? ""} />;
}
