"use client";

import { useRef } from "react";
import { cn } from "@/lib/utils";

export interface OtpDigitInputProps {
  value: string;
  onChange: (value: string) => void;
  length?: number;
  disabled?: boolean;
  "aria-label"?: string;
}

export function OtpDigitInput({ value, onChange, length = 6, disabled, ...aria }: OtpDigitInputProps) {
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);
  const digits = Array.from({ length }, (_, i) => value[i] ?? "");

  const setDigit = (index: number, digit: string) => {
    const next = digits.slice();
    next[index] = digit;
    onChange(next.join(""));
  };

  const handleChange = (index: number, raw: string) => {
    const incoming = raw.replace(/\D/g, "");

    if (incoming.length > 1) {
      // A paste landed in one box — spread it across the remaining boxes.
      const next = value.split("");
      for (let i = 0; i < incoming.length && index + i < length; i++) {
        next[index + i] = incoming[i]!;
      }
      onChange(next.join("").slice(0, length));
      const lastFilled = Math.min(index + incoming.length, length - 1);
      inputRefs.current[lastFilled]?.focus();
      return;
    }

    setDigit(index, incoming);
    if (incoming && index < length - 1) {
      inputRefs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (index: number, event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Backspace" && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  };

  return (
    <div className="flex gap-2" role="group" aria-label={aria["aria-label"]}>
      {digits.map((digit, index) => (
        <input
          key={index}
          ref={(el) => {
            inputRefs.current[index] = el;
          }}
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={length}
          disabled={disabled}
          value={digit}
          onChange={(event) => handleChange(index, event.target.value)}
          onKeyDown={(event) => handleKeyDown(index, event)}
          aria-label={`Digit ${index + 1}`}
          className={cn(
            "h-14 w-12 rounded-md border border-line-strong bg-surface text-center text-xl font-bold text-ink outline-none",
            "focus-visible:border-green-600 focus-visible:ring-2 focus-visible:ring-green-600/40",
            "disabled:cursor-not-allowed disabled:opacity-50",
          )}
        />
      ))}
    </div>
  );
}
