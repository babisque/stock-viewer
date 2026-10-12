import type { Market } from "@stock-viewer/shared";

// The only place chart colours are defined. Values are CSS variables from
// index.css, so each theme gets its own validated steps. Slots are assigned
// in order, never cycled: a chart has at most 7 coloured slices + "Other".
export const CHART_COLORS = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
  "var(--chart-6)",
  "var(--chart-7)",
] as const;

export const OTHER_COLOR = "var(--muted-foreground)";

export const MARKET_COLORS: Record<Market, string> = {
  B3: CHART_COLORS[0],
  US: CHART_COLORS[1],
};

// Gap between slices, matching the card behind the chart.
export const SLICE_GAP_COLOR = "var(--card)";
