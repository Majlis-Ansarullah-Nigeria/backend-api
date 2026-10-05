import { AlertCircle, Check, Loader2, WifiOff } from "lucide-react";
import { cn } from "@/lib/utils";

export type SaveStatus = "saving" | "saved" | "offline" | "error";

export interface SaveIndicatorProps {
  status: SaveStatus;
  /** For status="saved": a pre-formatted relative time, e.g. "8 seconds ago". */
  savedLabel?: string;
  className?: string;
}

const STATUS_TEXT: Record<SaveStatus, string> = {
  saving: "Saving…",
  saved: "Saved",
  offline: "Offline — changes saved on this device",
  error: "Couldn't save — retry",
};

export function SaveIndicator({ status, savedLabel, className }: SaveIndicatorProps) {
  const text =
    status === "saved" && savedLabel ? `Saved ${savedLabel} ago` : STATUS_TEXT[status];

  return (
    <span
      role="status"
      className={cn(
        "inline-flex items-center gap-1.5 text-sm font-medium",
        status === "saved" && "text-green-700",
        status === "saving" && "text-ink-muted",
        status === "offline" && "text-status-late-fg",
        status === "error" && "text-danger",
        className,
      )}
    >
      {status === "saving" && <Loader2 className="size-4 shrink-0 animate-spin" aria-hidden="true" />}
      {status === "saved" && <Check className="size-4 shrink-0" aria-hidden="true" />}
      {status === "offline" && <WifiOff className="size-4 shrink-0" aria-hidden="true" />}
      {status === "error" && <AlertCircle className="size-4 shrink-0" aria-hidden="true" />}
      {text}
    </span>
  );
}
