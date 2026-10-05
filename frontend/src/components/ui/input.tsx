import type * as React from "react";
import { cn } from "@/lib/utils";

export function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        "h-11 w-full min-w-0 rounded-sm border border-line-strong bg-surface px-3 text-base text-ink shadow-xs outline-none transition-colors placeholder:text-ink-muted disabled:cursor-not-allowed disabled:opacity-50",
        "focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40",
        "aria-invalid:border-danger aria-invalid:ring-danger/20",
        className,
      )}
      {...props}
    />
  );
}
