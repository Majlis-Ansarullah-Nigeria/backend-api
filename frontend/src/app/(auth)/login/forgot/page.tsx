import { AuthSplitLayout } from "@/features/identity/components/AuthSplitLayout";
import { ForgotPasswordForm } from "@/features/identity/components/ForgotPasswordForm";

export default function ForgotPasswordPage() {
  return (
    <AuthSplitLayout>
      <ForgotPasswordForm />
    </AuthSplitLayout>
  );
}
