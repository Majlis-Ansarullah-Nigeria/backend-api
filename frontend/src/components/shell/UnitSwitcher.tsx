"use client";

import { ChevronDown } from "lucide-react";
import { useTranslations } from "next-intl";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

export interface UnitOption {
  id: string;
  name: string;
  role: string;
}

export interface UnitSwitcherProps {
  current: UnitOption;
  /** Other units/roles this user can act as. Defaults to just the current one until RoleAssignment (M1/M4) is wired up. */
  options?: UnitOption[];
  onSelect?: (unit: UnitOption) => void;
}

export function UnitSwitcher({ current, options = [current], onSelect }: UnitSwitcherProps) {
  const t = useTranslations("Shell");

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="flex w-full items-center justify-between rounded-md bg-white/5 px-3 py-2.5 text-left transition-colors hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-lime">
        <span>
          <span className="block text-[11px] font-semibold uppercase tracking-wide text-white/60">
            {t("actingAs")}
          </span>
          <span className="block text-sm font-bold text-white">{current.name}</span>
          <span className="block text-xs text-white/60">{current.role}</span>
        </span>
        <ChevronDown className="size-4 shrink-0 text-white/60" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel>{t("switchUnit")}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {options.map((unit) => (
          <DropdownMenuItem key={unit.id} onSelect={() => onSelect?.(unit)}>
            <span className="flex flex-col">
              <span className="font-medium">{unit.name}</span>
              <span className="text-xs text-ink-muted">{unit.role}</span>
            </span>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
