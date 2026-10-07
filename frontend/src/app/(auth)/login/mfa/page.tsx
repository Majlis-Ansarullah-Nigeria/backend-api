import { Suspense } from "react";
import { AuthSplitLayout } from "@/features/identity/components/AuthSplitLayout";
import { MfaChallengeForm } from "@/features/identity/components/MfaChallengeForm";

export default function LoginMfaPage() {
  return (
    <AuthSplitLayout>
      <Suspense>
        <MfaChallengeForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
