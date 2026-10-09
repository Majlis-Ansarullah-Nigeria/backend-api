import { ReviewScreen } from "@/features/submissions/components/ReviewScreen";

export default async function ReportReviewPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ unit?: string }>;
}) {
  const { id } = await params;
  const { unit } = await searchParams;

  return <ReviewScreen id={id} unitId={unit ?? ""} />;
}
