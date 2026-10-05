import { AcceptInvitationForm } from "@/features/identity/components/AcceptInvitationForm";

export default async function AcceptInvitationPage({
  params,
}: {
  params: Promise<{ token: string }>;
}) {
  const { token } = await params;

  return <AcceptInvitationForm token={token} />;
}
