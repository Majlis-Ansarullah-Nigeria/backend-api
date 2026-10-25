import { cn } from "@/lib/utils";

/** The organization's own logo, shipped with the app; FR-ADM-001's uploaded logo replaces it. */
export const DEFAULT_LOGO_SRC = "/brand/logo.jpg";

export interface BrandMarkProps {
  /** The logo uploaded in Settings, if any. */
  logoUrl?: string | null;
  className?: string;
}

/**
 * The logo beside the app and organization name. Decorative (empty alt): the name next to it
 * already says whose service this is.
 */
export function BrandMark({ logoUrl, className }: BrandMarkProps) {
  return (
    // eslint-disable-next-line @next/next/no-img-element -- the uploaded logo is served by the API with its own caching
    <img src={logoUrl || DEFAULT_LOGO_SRC} alt="" className={cn("size-10 shrink-0 rounded-full bg-white object-contain", className)} />
  );
}
