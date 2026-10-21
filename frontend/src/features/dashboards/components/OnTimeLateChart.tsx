"use client";

import { Bar, BarChart, CartesianGrid, Cell, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

export interface OnTimeLatePoint {
  periodLabel: string;
  /** Percent of expected units, 0–100. */
  onTime: number;
  late: number;
  /** A period still open: shown as one paler "submitted so far" bar. */
  isOpen: boolean;
}

/** FR-DSH-001's "On time and late, by period": stacked on-time (bottom) and late (top) shares of
 * the expected units, with the still-open period as a single paler bar. Fixed stack order, a
 * legend, 2px segment gaps and a tooltip back up the two colours (dataviz CVD rule). */
export function OnTimeLateChart({
  points,
  labels,
}: {
  points: OnTimeLatePoint[];
  labels: { onTime: string; late: string; open: string; submittedSoFar: string; ariaLabel: string };
}) {
  return (
    <div className="flex flex-col gap-3">
      <ul className="flex justify-end gap-4 text-sm text-ink-muted" aria-hidden="true">
        <li className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-chart-on-time" />
          {labels.onTime}
        </li>
        <li className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-chart-late" />
          {labels.late}
        </li>
      </ul>
      <div role="img" aria-label={labels.ariaLabel} className="h-72 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={points} margin={{ top: 24, right: 8, bottom: 0, left: 0 }} barCategoryGap="35%">
            <CartesianGrid vertical={false} stroke="var(--color-line)" />
            <XAxis
              dataKey="periodLabel"
              tickLine={false}
              axisLine={{ stroke: "var(--color-line-strong)" }}
              tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }}
            />
            <YAxis
              domain={[0, 100]}
              ticks={[0, 25, 50, 75, 100]}
              width={44}
              tickLine={false}
              axisLine={false}
              tickFormatter={(value: number) => `${value}%`}
              tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }}
            />
            <Tooltip
              cursor={{ fill: "var(--color-ground)" }}
              formatter={(value, name, item) => {
                const point = item.payload as OnTimeLatePoint;
                if (point.isOpen) {
                  return name === "onTime" ? [`${value}%`, labels.submittedSoFar] : [];
                }
                return [`${value}%`, name === "onTime" ? labels.onTime : labels.late];
              }}
              contentStyle={{ borderRadius: 10, borderColor: "var(--color-line)", color: "var(--color-ink)" }}
            />
            <Bar dataKey="onTime" stackId="share" isAnimationActive={false} stroke="var(--color-surface)" strokeWidth={2}>
              {points.map((point) => (
                <Cell key={point.periodLabel} fill={point.isOpen ? "var(--color-chart-open)" : "var(--color-chart-on-time)"} />
              ))}
            </Bar>
            <Bar dataKey="late" stackId="share" isAnimationActive={false} radius={[4, 4, 0, 0]} stroke="var(--color-surface)" strokeWidth={2} fill="var(--color-chart-late)">
              <LabelList
                dataKey="isOpen"
                position="top"
                content={(props) => {
                  const { x, y, width, index } = props as { x?: number; y?: number; width?: number; index?: number };
                  const point = index == null ? undefined : points[index];
                  if (!point?.isOpen || x == null || y == null || width == null) {
                    return null;
                  }
                  return (
                    <text x={x + width / 2} y={y - 8} textAnchor="middle" className="fill-ink text-xs font-bold">
                      {labels.open}
                    </text>
                  );
                }}
              />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
