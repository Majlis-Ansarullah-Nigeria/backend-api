import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

export type BulkImportStep = "upload" | "columns" | "rows" | "done";

const STEPS: { key: BulkImportStep; label: string }[] = [
  { key: "upload", label: "Upload file" },
  { key: "columns", label: "Match columns" },
  { key: "rows", label: "Check rows" },
  { key: "done", label: "Import" },
];

export function BulkImportStepper({ current }: { current: BulkImportStep }) {
  const currentIndex = STEPS.findIndex((step) => step.key === current);

  return (
    <ol className="flex flex-wrap items-center gap-3 rounded-lg border border-line bg-surface p-4">
      {STEPS.map((step, index) => {
        const done = index < currentIndex;
        const active = index === currentIndex;

        return (
          <li key={step.key} className="flex items-center gap-2">
            {index > 0 ? <span className="h-px w-6 bg-line-strong" aria-hidden="true" /> : null}
            <span
              className={cn(
                "flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold",
                done && "bg-green-600 text-white",
                active && "bg-lime text-forest-900",
                !done && !active && "border border-line-strong text-ink-muted",
              )}
            >
              {done ? <Check className="size-3.5" aria-hidden="true" /> : index + 1}
            </span>
            <span className={cn("text-sm font-medium", active ? "text-ink" : "text-ink-muted")}>{step.label}</span>
          </li>
        );
      })}
    </ol>
  );
}
