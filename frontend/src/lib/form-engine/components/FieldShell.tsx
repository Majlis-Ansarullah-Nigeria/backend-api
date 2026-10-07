import type { ReactNode } from "react";
import { Label } from "@/components/ui/label";

export function FieldShell({
  fieldKey,
  label,
  helpText,
  required,
  error,
  children,
}: {
  fieldKey: string;
  label: string;
  helpText?: string | null;
  required: boolean;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={fieldKey}>
        {label}
        {required ? <span className="ml-0.5 text-danger">*</span> : null}
      </Label>
      {children}
      {helpText ? <p className="text-xs text-ink-muted">{helpText}</p> : null}
      {error ? <p className="text-xs text-danger">{error}</p> : null}
    </div>
  );
}
