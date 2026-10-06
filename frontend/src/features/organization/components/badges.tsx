import { AlertTriangle } from "lucide-react";
import { cn } from "@/lib/utils";
import { UNIT_LEVEL_CODE } from "@/features/organization/utils/tree";

const LEVEL_BADGE_STYLES: Record<number, string> = {
  [UNIT_LEVEL_CODE.National]: "bg-forest-900 text-white",
  [UNIT_LEVEL_CODE.Zone]: "bg-green-600 text-white",
  [UNIT_LEVEL_CODE.Dila]: "bg-mint text-green-700",
  [UNIT_LEVEL_CODE.Muqam]: "bg-line text-ink-muted",
  [UNIT_LEVEL_CODE.Department]: "bg-status-upcoming-bg text-status-upcoming-fg",
};

export function LevelBadge({ code, label }: { code: number; label: string }) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-xs font-semibold",
        LEVEL_BADGE_STYLES[code] ?? "bg-line text-ink-muted",
      )}
    >
      {label}
    </span>
  );
}

export function VacantHeadBadge({ label }: { label: string }) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-status-returned-bg px-2 py-0.5 text-xs font-medium text-status-returned-fg">
      <AlertTriangle className="size-3.5" aria-hidden="true" />
      {label}
    </span>
  );
}
