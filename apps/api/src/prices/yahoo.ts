import { currencyForMarket, type Market } from "@stock-viewer/shared";
import { z } from "zod";
import {
  priceToString,
  providerError,
  type PriceProvider,
  type Quote,
  type QuoteRequest,
} from "./types";

// The yahoo-finance2 `quote` call, injected so tests never reach Yahoo.
export type YahooQuoteFn = (symbols: string[]) => Promise<unknown>;

const YahooQuotesSchema = z.array(
  z.object({
    symbol: z.string(),
    regularMarketPrice: z.number().positive().optional(),
    currency: z.string().optional(),
  }),
);

export function toYahooSymbol(ticker: string, market: Market): string {
  return market === "B3" ? `${ticker}.SA` : ticker;
}

export function createYahooProvider({
  quote,
  now = () => new Date(),
}: {
  quote: YahooQuoteFn;
  now?: () => Date;
}): PriceProvider {
  return {
    name: "yahoo",
    supports: () => true,
    async getQuotes(items: QuoteRequest[]): Promise<Quote[]> {
      const bySymbol = new Map(
        items.map((item) => [toYahooSymbol(item.ticker, item.market), item]),
      );

      let raw: unknown;
      try {
        raw = await quote([...bySymbol.keys()]);
      } catch (error) {
        throw providerError("yahoo", "request failed", error);
      }

      const parsed = YahooQuotesSchema.safeParse(raw);
      if (!parsed.success) {
        throw providerError("yahoo", "unexpected response", parsed.error);
      }

      const fetchedAt = now().toISOString();
      const quotes: Quote[] = [];
      for (const result of parsed.data) {
        const item = bySymbol.get(result.symbol);
        if (!item || result.regularMarketPrice === undefined) continue;
        const currency = currencyForMarket(item.market);
        // A different currency would make the profit math wrong: skip it.
        if (result.currency !== undefined && result.currency !== currency) {
          continue;
        }
        quotes.push({
          ticker: item.ticker,
          market: item.market,
          price: priceToString(result.regularMarketPrice),
          currency,
          source: "yahoo",
          fetchedAt,
        });
      }
      return quotes;
    },
  };
}
