import { WindowFormScreen } from "@/features/windows/components/WindowFormScreen";

export default async function EditWindowPage({
  params,
  searchParams,
}: {
  params: Promise<{ windowId: string }>;
  searchParams: Promise<{ reportTypeId?: string }>;
}) {
  const { windowId } = await params;
  const { reportTypeId } = await searchParams;

  return <WindowFormScreen windowId={windowId} reportTypeIdParam={reportTypeId} />;
}
