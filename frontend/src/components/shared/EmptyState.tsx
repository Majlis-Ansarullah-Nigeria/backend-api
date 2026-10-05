import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description?: string;
  action?: ReactNode;
  /** Background behind the icon. Defaults to mint (neutral/positive); pass "bg-status-upcoming-bg" etc. for context. */
  iconClassName?: string;
  className?: string;
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  iconClassName,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center gap-3 rounded-lg border border-line bg-surface px-6 py-12 text-center",
        className,
      )}
    >
      <span
        className={cn(
          "flex size-14 items-center justify-center rounded-full bg-mint text-green-600",
          iconClassName,
        )}
      >
        <Icon className="size-6" aria-hidden="true" />
      </span>
      <h3 className="text-lg font-semibold text-ink">{title}</h3>
      {description ? <p className="max-w-sm text-sm text-ink-muted">{description}</p> : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
