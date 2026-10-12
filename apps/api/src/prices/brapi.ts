import { z } from "zod";
import {
  priceToString,
  providerError,
  type PriceProvider,
  type Quote,
  type QuoteRequest,
} from "./types";

export const BRAPI_URL = "https://brapi.dev/api/quote";
const TIMEOUT_MS = 5_000;

const BrapiResponseSchema = z.object({
  results: z.array(
    z.object({
      symbol: z.string(),
      regularMarketPrice: z.number().positive().nullish(),
      currency: z.string().nullish(),
    }),
  ),
});

// B3-only fallback. The token is optional (the free tier works without one
// for a limited set of tickers).
export function createBrapiProvider({
  token,
  now = () => new Date(),
}: {
  token?: string;
  now?: () => Date;
} = {}): PriceProvider {
  return {
    name: "brapi",
    supports: (market) => market === "B3",
    async getQuotes(items: QuoteRequest[]): Promise<Quote[]> {
      const byTicker = new Map(items.map((item) => [item.ticker, item]));
      const tickers = [...byTicker.keys()].map(encodeURIComponent).join(",");

      let response: Response;
      try {
        response = await fetch(`${BRAPI_URL}/${tickers}`, {
          headers: token ? { authorization: `Bearer ${token}` } : undefined,
          signal: AbortSignal.timeout(TIMEOUT_MS),
        });
      } catch (error) {
        throw providerError("brapi", "request failed", error);
      }
      if (!response.ok) {
        throw providerError("brapi", `HTTP ${response.status}`);
      }

      const body: unknown = await response.json().catch(() => null);
      const parsed = BrapiResponseSchema.safeParse(body);
      if (!parsed.success) {
        throw providerError("brapi", "unexpected response", parsed.error);
      }

      const fetchedAt = now().toISOString();
      const quotes: Quote[] = [];
      for (const result of parsed.data.results) {
        const item = byTicker.get(result.symbol);
        if (!item || result.regularMarketPrice == null) continue;
        if (result.currency != null && result.currency !== "BRL") continue;
        quotes.push({
          ticker: item.ticker,
          market: item.market,
          price: priceToString(result.regularMarketPrice),
          currency: "BRL",
          source: "brapi",
          fetchedAt,
        });
      }
      return quotes;
    },
  };
}
