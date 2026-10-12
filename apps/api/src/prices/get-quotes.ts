import { createTtlCache } from "./cache";
import {
  quoteKey,
  type Logger,
  type PriceProvider,
  type Quote,
  type QuoteRequest,
} from "./types";

export const QUOTE_TTL_MS = 5 * 60 * 1000;

type QuoteServiceOptions = {
  // In fallback order; each market uses the providers that support it
  // (B3 → yahoo, brapi; US → yahoo).
  providers: PriceProvider[];
  log: Logger;
  now?: () => number;
};

function uniqueItems(items: QuoteRequest[]): QuoteRequest[] {
  return [...new Map(items.map((item) => [quoteKey(item), item])).values()];
}

export function createQuoteService({
  providers,
  log,
  now = Date.now,
}: QuoteServiceOptions) {
  const cache = createTtlCache<Quote>({ ttlMs: QUOTE_TTL_MS, now });

  // Returns quotes for the tickers that some provider could price. Never
  // throws because of a provider: failures are logged and the next one tried.
  async function getQuotes(items: QuoteRequest[]): Promise<Quote[]> {
    const quotes: Quote[] = [];
    let pending: QuoteRequest[] = [];

    for (const item of uniqueItems(items)) {
      const cached = cache.get(quoteKey(item));
      if (cached) quotes.push(cached);
      else pending.push(item);
    }

    for (const provider of providers) {
      const batch = pending.filter((item) => provider.supports(item.market));
      if (batch.length === 0) continue;

      const requested = new Set(batch.map(quoteKey));
      const found = new Set<string>();
      try {
        for (const quote of await provider.getQuotes(batch)) {
          const key = quoteKey(quote);
          if (!requested.has(key) || found.has(key)) continue;
          found.add(key);
          cache.set(key, quote);
          quotes.push(quote);
        }
      } catch (error) {
        log.warn(
          {
            provider: provider.name,
            tickers: batch.map((i) => i.ticker),
            err: error,
          },
          "Price provider failed",
        );
        continue;
      }

      const missing = batch.filter((item) => !found.has(quoteKey(item)));
      if (missing.length > 0) {
        log.warn(
          { provider: provider.name, tickers: missing.map((i) => i.ticker) },
          "Price provider returned no quote",
        );
      }
      pending = pending.filter((item) => !found.has(quoteKey(item)));
    }

    return quotes;
  }

  return {
    getQuotes,
    // Next getQuotes() call goes to the providers again.
    clear: () => cache.expireAll(),
  };
}

export type QuoteService = ReturnType<typeof createQuoteService>;
