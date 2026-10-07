import { Suspense } from "react";
import { AuthSplitLayout } from "@/features/identity/components/AuthSplitLayout";
import { CodeVerifyForm } from "@/features/identity/components/CodeVerifyForm";

export default function LoginCodePage() {
  return (
    <AuthSplitLayout>
      <Suspense>
        <CodeVerifyForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
