import { currencyForMarket, type Market } from "@stock-viewer/shared";
import { vi } from "vitest";
import type { PriceProvider, QuoteRequest } from "../src/prices/types";

export const FIXED_TIME = "2026-10-12T14:32:00.000Z";

// A provider that prices tickers from a fixed table and counts its calls.
// Set `fail` to make it throw like a provider outage.
export function fakeProvider(
  name: PriceProvider["name"],
  prices: Record<string, string>,
  options: { markets?: Market[]; fail?: boolean } = {},
) {
  const markets = options.markets ?? ["B3", "US"];
  const state = { fail: options.fail ?? false };
  const getQuotes = vi.fn(async (items: QuoteRequest[]) => {
    if (state.fail) throw new Error(`${name} is down`);
    return items.flatMap((item) => {
      const price = prices[item.ticker];
      if (price === undefined) return [];
      return [
        {
          ...item,
          price,
          currency: currencyForMarket(item.market),
          source: name,
          fetchedAt: FIXED_TIME,
        },
      ];
    });
  });
  const provider: PriceProvider = {
    name,
    supports: (market) => markets.includes(market),
    getQuotes,
  };
  return { provider, getQuotes, state };
}

export function fakeLogger() {
  return { warn: vi.fn() };
}
