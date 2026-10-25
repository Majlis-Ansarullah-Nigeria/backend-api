"use client";

import { Bell, Search } from "lucide-react";
import { useTranslations } from "next-intl";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";

export interface TopBarProps {
  title: string;
  user: { name: string; initials: string };
  notificationCount?: number;
  /** Opens the search palette (FR-SRC-001); Ctrl/Cmd+K does the same from anywhere. */
  onOpenSearch?: () => void;
}

export function TopBar({ title, user, notificationCount = 0, onOpenSearch }: TopBarProps) {
  const t = useTranslations("Shell");

  return (
    <header className="hidden h-16 shrink-0 items-center justify-between gap-4 border-b border-line bg-surface px-6 md:flex">
      {/* Not an <h1>: this is persistent chrome (hidden on mobile), not the page's content heading. */}
      <p className="text-base font-semibold text-ink">{title}</p>
      <div className="flex flex-1 items-center justify-end gap-3">
        <button
          type="button"
          onClick={onOpenSearch}
          aria-keyshortcuts="Control+K Meta+K"
          className="flex w-full max-w-sm items-center gap-2 rounded-md border border-line-strong bg-ground px-3 py-2 text-left text-sm text-ink-muted transition-colors hover:border-green-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-600"
        >
          <Search className="size-4 shrink-0" aria-hidden="true" />
          <span className="flex-1 truncate">{t("search")}</span>
          <kbd className="rounded border border-line-strong bg-surface px-1.5 py-0.5 text-xs">Ctrl K</kbd>
        </button>
        <button
          type="button"
          aria-label={notificationCount > 0 ? t("notificationsUnread", { count: notificationCount }) : t("notifications")}
          className="relative rounded-md p-2 text-ink-muted transition-colors hover:bg-ground"
        >
          <Bell className="size-5" aria-hidden="true" />
          {notificationCount > 0 ? (
            <Badge variant="danger" className="absolute -right-1 -top-1">
              {notificationCount}
            </Badge>
          ) : null}
        </button>
        <Avatar>
          <AvatarFallback>{user.initials}</AvatarFallback>
        </Avatar>
      </div>
    </header>
  );
}
