import {
  AlertTriangle,
  Calendar,
  Check,
  Clock,
  Eye,
  Pencil,
  Send,
  Undo2,
  X,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";

export type StatusChipVariant =
  | "not-started"
  | "draft"
  | "submitted"
  | "in-review"
  | "late"
  | "returned"
  | "acknowledged"
  | "missed"
  | "overdue"
  | "upcoming";

const STATUS_CONFIG: Record<
  StatusChipVariant,
  { label: string; icon: LucideIcon; bg: string; fg: string }
> = {
  "not-started": { label: "Not started", icon: Clock, bg: "bg-status-not-started-bg", fg: "text-status-not-started-fg" },
  draft: { label: "Draft", icon: Pencil, bg: "bg-status-draft-bg", fg: "text-status-draft-fg" },
  submitted: { label: "Submitted", icon: Send, bg: "bg-status-submitted-bg", fg: "text-status-submitted-fg" },
  "in-review": { label: "In review", icon: Eye, bg: "bg-status-in-review-bg", fg: "text-status-in-review-fg" },
  late: { label: "Submitted late", icon: Clock, bg: "bg-status-late-bg", fg: "text-status-late-fg" },
  returned: { label: "Returned", icon: Undo2, bg: "bg-status-returned-bg", fg: "text-status-returned-fg" },
  acknowledged: { label: "Acknowledged", icon: Check, bg: "bg-status-acknowledged-bg", fg: "text-status-acknowledged-fg" },
  missed: { label: "Missed", icon: X, bg: "bg-status-missed-bg", fg: "text-status-missed-fg" },
  overdue: { label: "Overdue", icon: AlertTriangle, bg: "bg-status-overdue-bg", fg: "text-status-overdue-fg" },
  upcoming: { label: "Upcoming", icon: Calendar, bg: "bg-status-upcoming-bg", fg: "text-status-upcoming-fg" },
};

export interface StatusChipProps {
  status: StatusChipVariant;
  /** Override the default label (e.g. to show a localized or more specific word). */
  label?: string;
  className?: string;
}

export function StatusChip({ status, label, className }: StatusChipProps) {
  const config = STATUS_CONFIG[status];
  const Icon = config.icon;

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-sm font-medium",
        config.bg,
        config.fg,
        className,
      )}
    >
      <Icon className="size-3.5 shrink-0" aria-hidden="true" />
      {label ?? config.label}
    </span>
  );
}
