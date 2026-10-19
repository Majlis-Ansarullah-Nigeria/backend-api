"use client";

import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { formatFigure } from "@/features/history/utils/comparison";

export interface TrendPoint {
  periodLabel: string;
  value: number | null;
}

/** FR-HIS-005's 12-period trend: one series, so the card title names it and there's no legend
 * box. The comparison table below the chart doubles as its table view. */
export function TrendChart({ points, fieldType, ariaLabel }: { points: TrendPoint[]; fieldType: number; ariaLabel: string }) {
  const last = points.length - 1;

  return (
    <div role="img" aria-label={ariaLabel} className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={points} margin={{ top: 24, right: 24, bottom: 8, left: 0 }}>
          <CartesianGrid vertical={false} stroke="var(--color-line)" />
          <XAxis
            dataKey="periodLabel"
            tickLine={false}
            axisLine={{ stroke: "var(--color-line-strong)" }}
            tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }}
          />
          <YAxis
            width={56}
            tickLine={false}
            axisLine={false}
            tick={{ fill: "var(--color-ink-muted)", fontSize: 12 }}
            tickFormatter={(value: number) => formatFigure(value, fieldType)}
          />
          <Tooltip
            cursor={{ stroke: "var(--color-line-strong)" }}
            formatter={(value) => formatFigure(typeof value === "number" ? value : null, fieldType)}
            contentStyle={{ borderRadius: 10, borderColor: "var(--color-line)", color: "var(--color-ink)" }}
          />
          <Line
            type="linear"
            dataKey="value"
            stroke="var(--color-green-600)"
            strokeWidth={2}
            connectNulls
            isAnimationActive={false}
            dot={(props: { cx?: number; cy?: number; index?: number }) => (
              <circle
                key={props.index}
                cx={props.cx}
                cy={props.cy}
                r={props.index === last ? 5 : 4}
                fill={props.index === last ? "var(--color-green-600)" : "var(--color-surface)"}
                stroke="var(--color-green-600)"
                strokeWidth={2}
              />
            )}
            label={(props: { x?: number | string; y?: number | string; index?: number }) =>
              props.index === last && points[last]?.value != null ? (
                <text
                  key="last-label"
                  x={Number(props.x)}
                  y={Number(props.y) - 12}
                  textAnchor="end"
                  className="fill-ink text-xs font-bold"
                >
                  {`${formatFigure(points[last].value, fieldType)} · ${points[last].periodLabel}`}
                </text>
              ) : (
                <g key={`label-${props.index}`} />
              )
            }
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
