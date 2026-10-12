import { formatMoney, formatShare, type Currency } from "@stock-viewer/shared";
import { useId } from "react";
import { Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import type { ChartSlice } from "./allocation-data";
import { SLICE_GAP_COLOR } from "./chart-palette";

type AllocationChartProps = {
  title: string;
  slices: ChartSlice[];
  note: string | null;
  currency: Currency;
};

function Swatch({ color }: { color: string }) {
  return (
    <span
      aria-hidden
      className="inline-block size-3 shrink-0 rounded-sm"
      style={{ backgroundColor: color }}
    />
  );
}

function SliceTooltip({
  active,
  payload,
  currency,
}: {
  active?: boolean;
  payload?: readonly { payload?: unknown }[];
  currency: Currency;
}) {
  const slice = payload?.[0]?.payload as ChartSlice | undefined;
  if (!active || !slice) return null;
  return (
    <div className="bg-popover text-popover-foreground rounded-md border px-3 py-2 text-xs shadow-md">
      <div className="flex items-center gap-2 font-medium">
        <Swatch color={slice.fill} />
        {slice.label}
      </div>
      <div className="mt-1 tabular-nums">
        {formatMoney(slice.value, currency)} · {formatShare(slice.percent)}
      </div>
    </div>
  );
}

// The legend is a real table with value and %, so the chart never relies on
// colour alone and screen readers get the same information.
export function AllocationLegend({
  slices,
  currency,
}: Pick<AllocationChartProps, "slices" | "currency">) {
  return (
    <table className="w-full text-sm">
      <thead className="sr-only">
        <tr>
          <th scope="col">Name</th>
          <th scope="col">Value</th>
          <th scope="col">Share</th>
        </tr>
      </thead>
      <tbody>
        {slices.map((slice) => (
          <tr key={slice.key} className="tabular-nums">
            <th scope="row" className="py-0.5 text-left font-medium">
              <span className="flex items-center gap-2">
                <Swatch color={slice.fill} />
                {slice.label}
              </span>
            </th>
            <td className="text-muted-foreground text-right">
              {formatMoney(slice.value, currency)}
            </td>
            <td className="w-20 text-right">{formatShare(slice.percent)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function AllocationChart({
  title,
  slices,
  note,
  currency,
}: AllocationChartProps) {
  const titleId = useId();
  const summary = slices
    .map((slice) => `${slice.label} ${formatShare(slice.percent)}`)
    .join(", ");

  return (
    <section
      aria-labelledby={titleId}
      className="bg-card text-card-foreground rounded-lg border p-4"
    >
      <h2 id={titleId} className="mb-3 text-sm font-medium">
        {title}
      </h2>
      {slices.length === 0 ? (
        <p className="text-muted-foreground py-8 text-center text-sm">
          No priced positions to show.
        </p>
      ) : (
        <div className="flex flex-col items-center gap-4 sm:flex-row">
          <div
            role="img"
            aria-label={`${title}: ${summary}`}
            className="size-40 shrink-0"
          >
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={slices}
                  dataKey="angle"
                  nameKey="label"
                  innerRadius="58%"
                  outerRadius="100%"
                  stroke={SLICE_GAP_COLOR}
                  strokeWidth={2}
                  isAnimationActive={false}
                />
                <Tooltip
                  content={({ active, payload }) => (
                    <SliceTooltip
                      active={active}
                      payload={payload}
                      currency={currency}
                    />
                  )}
                />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <AllocationLegend slices={slices} currency={currency} />
        </div>
      )}
      {note && <p className="text-muted-foreground mt-3 text-xs">{note}</p>}
    </section>
  );
}
