import { Suspense } from "react";
import { AuthSplitLayout } from "@/features/identity/components/AuthSplitLayout";
import { ResetPasswordForm } from "@/features/identity/components/ResetPasswordForm";

export default async function ResetPasswordPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = await params;

  return (
    <AuthSplitLayout>
      <Suspense>
        <ResetPasswordForm token={token} />
      </Suspense>
    </AuthSplitLayout>
  );
}
