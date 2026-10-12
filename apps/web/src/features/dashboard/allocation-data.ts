import {
  allocationSlices,
  groupOther,
  isClosedPosition,
  OTHER_KEY,
  type AllocationItem,
  type AllocationSlice,
  type Currency,
  type Portfolio,
} from "@stock-viewer/shared";
import { CHART_COLORS, MARKET_COLORS, OTHER_COLOR } from "./chart-palette";

export type ChartSlice = AllocationSlice & {
  fill: string;
  // Recharts needs a number for the slice angle; this is for drawing only,
  // every displayed value comes from the Decimals above.
  angle: number;
};

export type AllocationData = {
  slices: ChartSlice[];
  // Positions left out of the chart, and why.
  note: string | null;
};

const MAX_SLICES = CHART_COLORS.length + 1; // 7 positions + "Other"

function plural(count: number, word: string) {
  return `${count} ${word}${count === 1 ? "" : "s"}`;
}

// Closed positions (everything sold) have no value to show, so they are left
// out silently.
function unpricedNote(portfolio: Portfolio): string | null {
  const count = portfolio.positions.filter(
    (position) => position.currentValue === null && !isClosedPosition(position),
  ).length;
  return count === 0
    ? null
    : `${plural(count, "position")} without a price not included.`;
}

// Share of current value per market (B3 vs US). Null when there is no
// USD/BRL rate, because BRL and USD values can't be compared then.
export function marketAllocation(
  portfolio: Portfolio,
  currency: Currency,
): AllocationData | null {
  if (!portfolio.fx) return null;
  const items: AllocationItem[] = portfolio.positions.map((position) => ({
    key: position.market,
    label: position.market,
    currency: position.currency,
    value: position.currentValue,
  }));
  const slices = allocationSlices(items, currency, portfolio.fx.rate);
  return {
    slices: slices.map((slice) => ({
      ...slice,
      fill: MARKET_COLORS[slice.key as keyof typeof MARKET_COLORS],
      angle: slice.percent.toNumber(),
    })),
    note: unpricedNote(portfolio),
  };
}

// Share of current value per position: the 7 largest plus "Other".
// Without a rate, only positions already in `currency` can be shown.
export function positionAllocation(
  portfolio: Portfolio,
  currency: Currency,
): AllocationData {
  const rate = portfolio.fx?.rate ?? null;
  const tickers = portfolio.positions.map((position) => position.ticker);
  const positions =
    rate === null
      ? portfolio.positions.filter((position) => position.currency === currency)
      : portfolio.positions;

  const items: AllocationItem[] = positions.map((position) => ({
    key: position.id,
    // The same ticker can exist on both markets; tell them apart.
    label:
      tickers.filter((ticker) => ticker === position.ticker).length > 1
        ? `${position.ticker} (${position.market})`
        : position.ticker,
    currency: position.currency,
    value: position.currentValue,
  }));
  // Without a rate every item is already in `currency`, so the rate is unused.
  const slices = groupOther(
    allocationSlices(items, currency, rate ?? "1"),
    MAX_SLICES,
  );

  // Colours follow the position, not its rank: slots go in portfolio order,
  // so two positions swapping places doesn't repaint the chart.
  const order = portfolio.positions.map((position) => position.id);
  const colored = slices
    .filter((slice) => slice.key !== OTHER_KEY)
    .map((slice) => slice.key)
    .sort((a, b) => order.indexOf(a) - order.indexOf(b));

  const notes = [unpricedNote(portfolio)];
  if (rate === null) {
    notes.push(
      `Only ${currency} positions are shown: the USD/BRL rate is unavailable.`,
    );
  }

  return {
    slices: slices.map((slice) => ({
      ...slice,
      fill:
        slice.key === OTHER_KEY
          ? OTHER_COLOR
          : CHART_COLORS[colored.indexOf(slice.key)]!,
      angle: slice.percent.toNumber(),
    })),
    note: notes.filter(Boolean).join(" ") || null,
  };
}
