import Decimal from "decimal.js";
import type { Market, Quote, QuoteSource } from "@stock-viewer/shared";

export type { Quote };

export type QuoteRequest = { ticker: string; market: Market };

export type PriceProvider = {
  name: QuoteSource;
  supports: (market: Market) => boolean;
  // Returns quotes for the tickers it could price; unknown tickers are simply
  // left out. Throws a ProviderError when the whole call fails.
  getQuotes: (items: QuoteRequest[]) => Promise<Quote[]>;
};

// Minimal logger shape; Fastify's (pino) logger satisfies it.
export type Logger = {
  warn: (details: object, message: string) => void;
};

export type ProviderError = Error & { provider: string };

export function providerError(
  provider: string,
  message: string,
  cause?: unknown,
): ProviderError {
  return Object.assign(new Error(`${provider}: ${message}`, { cause }), {
    provider,
    isProviderError: true,
  });
}

export function isProviderError(error: unknown): error is ProviderError {
  return (
    error instanceof Error &&
    "isProviderError" in error &&
    error.isProviderError === true
  );
}

export function quoteKey(item: QuoteRequest): string {
  return `${item.market}:${item.ticker}`;
}

// Providers send prices as JS numbers; turn them into decimal strings right at
// the boundary so no float math happens afterwards.
export function priceToString(price: number): string {
  return new Decimal(price).toString();
}
