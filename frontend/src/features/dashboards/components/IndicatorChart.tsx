"use client";

import { Area, Bar, CartesianGrid, ComposedChart, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

export interface IndicatorPoint {
  periodLabel: string;
  value: number | null;
}

/** FR-DSH-002's trend for one figure: a line (with a soft area) or bars, plus a dashed target
 * line when the field has a target (FR-TPL-011). One series — the card title names it. */
export function IndicatorChart({
  points,
  target,
  mode,
  format,
  labels,
}: {
  points: IndicatorPoint[];
  target: number | null;
  mode: "line" | "bars";
  format: (value: number) => string;
  labels: { ariaLabel: string; target: string };
}) {
  return (
    <div role="img" aria-label={labels.ariaLabel} className="h-72 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={points} margin={{ top: 28, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke="var(--color-line)" />
          <XAxis dataKey="periodLabel" tickLine={false} axisLine={{ stroke: "var(--color-line-strong)" }} tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }} />
          <YAxis width={64} tickLine={false} axisLine={false} tickFormatter={format} tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }} />
          <Tooltip
            cursor={mode === "bars" ? { fill: "var(--color-ground)" } : { stroke: "var(--color-line-strong)" }}
            formatter={(value) => (typeof value === "number" ? format(value) : "—")}
            contentStyle={{ borderRadius: 10, borderColor: "var(--color-line)", color: "var(--color-ink)" }}
          />
          {target != null ? (
            <ReferenceLine
              y={target}
              stroke="var(--color-status-late-fg)"
              strokeDasharray="6 6"
              label={{ value: labels.target, position: "insideTopRight", fill: "var(--color-status-late-fg)", fontSize: 12, fontWeight: 700 }}
            />
          ) : null}
          {mode === "bars" ? (
            <Bar dataKey="value" fill="var(--color-chart-on-time)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
          ) : (
            <>
              <Area type="linear" dataKey="value" fill="var(--color-mint)" stroke="none" connectNulls isAnimationActive={false} />
              <Line
                type="linear"
                dataKey="value"
                stroke="var(--color-green-600)"
                strokeWidth={2}
                connectNulls
                isAnimationActive={false}
                dot={{ r: 4, fill: "var(--color-surface)", stroke: "var(--color-green-600)", strokeWidth: 2 }}
              />
            </>
          )}
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}
