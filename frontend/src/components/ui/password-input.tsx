"use client";

import { Eye, EyeOff } from "lucide-react";
import type * as React from "react";
import { useState } from "react";
import { cn } from "@/lib/utils";
import { Input } from "./input";

export interface PasswordInputProps extends Omit<React.ComponentProps<typeof Input>, "type"> {
  showPasswordLabel: string;
  hidePasswordLabel: string;
}

export function PasswordInput({ showPasswordLabel, hidePasswordLabel, className, ...props }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="relative">
      <Input type={visible ? "text" : "password"} className={cn("pr-11", className)} {...props} />
      <button
        type="button"
        onClick={() => setVisible((current) => !current)}
        aria-label={visible ? hidePasswordLabel : showPasswordLabel}
        className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-ink-muted outline-none transition-colors hover:text-ink focus-visible:text-ink focus-visible:ring-2 focus-visible:ring-green-600/40"
      >
        {visible ? <EyeOff className="size-4" aria-hidden="true" /> : <Eye className="size-4" aria-hidden="true" />}
      </button>
    </div>
  );
}
