import { Eye } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export interface ReadOnlyBannerProps {
  children?: ReactNode;
  className?: string;
}

export function ReadOnlyBanner({ children, className }: ReadOnlyBannerProps) {
  return (
    <div
      role="status"
      className={cn(
        "flex items-center gap-2 rounded-md bg-status-in-review-bg px-4 py-3 text-sm font-medium text-status-in-review-fg",
        className,
      )}
    >
      <Eye className="size-4 shrink-0" aria-hidden="true" />
      {children ?? "Read-only view. You can see this report but not act on it."}
    </div>
  );
}
