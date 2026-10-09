import { SubmittedScreen } from "@/features/submissions/components/SubmittedScreen";

export default async function ReportSubmittedPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ unit?: string }>;
}) {
  const { id } = await params;
  const { unit } = await searchParams;

  return <SubmittedScreen id={id} unitId={unit ?? ""} />;
}
