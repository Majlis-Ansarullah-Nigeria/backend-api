import { AuthSplitLayout } from "@/features/identity/components/AuthSplitLayout";
import { LoginForm } from "@/features/identity/components/LoginForm";

export default function LoginPage() {
  return (
    <AuthSplitLayout>
      <LoginForm />
    </AuthSplitLayout>
  );
}
