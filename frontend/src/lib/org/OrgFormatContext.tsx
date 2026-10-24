"use client";

import { createContext, useContext, useMemo, type ReactNode } from "react";
import { createOrgFormatter, DEFAULT_ORG_FORMAT, type OrgFormatSettings, type OrgFormatter } from "./format";

const defaultFormatter = createOrgFormatter(DEFAULT_ORG_FORMAT);

const OrgFormatContext = createContext<OrgFormatter>(defaultFormatter);

/** Provided by the app shell from the organization's settings (FR-ADM-001). */
export function OrgFormatProvider({ settings, children }: { settings: OrgFormatSettings | null; children: ReactNode }) {
  const formatter = useMemo(
    () => (settings ? createOrgFormatter(settings) : defaultFormatter),
    [settings?.timeZone, settings?.dateFormat], // eslint-disable-line react-hooks/exhaustive-deps
  );
  return <OrgFormatContext.Provider value={formatter}>{children}</OrgFormatContext.Provider>;
}

/** Dates in the organization's time zone and format. Outside the shell: the defaults. */
export function useOrgFormat(): OrgFormatter {
  return useContext(OrgFormatContext);
}
