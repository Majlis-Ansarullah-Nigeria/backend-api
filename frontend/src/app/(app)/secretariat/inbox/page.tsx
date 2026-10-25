import { InboxScreen } from "@/features/review/components/InboxScreen";

export default async function InboxPage({ searchParams }: { searchParams: Promise<{ report?: string }> }) {
  const { report } = await searchParams;
  // Keyed so arriving from the search palette while already here re-selects the report.
  return <InboxScreen key={report ?? ""} initialReportId={report} />;
}
