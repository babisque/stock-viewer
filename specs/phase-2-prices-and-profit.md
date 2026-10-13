# Phase 2 Spec: Prices & Profit Calculation

> **Implemented in Node; ported to .NET in [`specs/migration/`](migration/README.md).** The Node/TypeScript code was removed in M4. Kept as history: the behaviour described here is what the .NET app implements, but stack and file references are the old Node ones.

## 1. Overview and Goal
* **Purpose:** Fetch current prices for every position from free sources, fetch the USD/BRL rate, and calculate profit per position and for the whole portfolio.
* **Success Criteria:**
  * The positions table shows current price, current value, profit (amount) and profit (%) for each position.
  * Portfolio totals are shown in BRL and in USD, converted with the current USD/BRL rate.
  * If Yahoo Finance fails for a B3 ticker, brapi.dev is used automatically.
  * If no provider can price a ticker, the row shows "price unavailable" and is excluded from totals (with a visible warning) — the app never crashes.
  * Every quote shows its source and timestamp ("Yahoo · 14:32").
  * Repeated requests within the cache TTL do not hit external providers (verified by tests).

## 2. Tech Stack and Architecture
Everything from Phase 1, plus:
* **Yahoo Finance:** `yahoo-finance2` (no key).
* **brapi.dev:** plain `fetch` to `https://brapi.dev/api/quote/{tickers}`, token from `BRAPI_TOKEN` env var (optional).
* **FX:** plain `fetch` to `https://economia.awesomeapi.com.br/json/last/USD-BRL` (no key).
* **Cache:** in-memory `Map` with TTL (quotes: 5 min, FX: 15 min). No Redis.
* **HTTP mocking in tests:** `msw` (Mock Service Worker) — tests never call real providers.

### Provider interface
```ts
// apps/api/src/prices/types.ts
export type Quote = {
  ticker: string;
  market: Market;
  price: string;        // decimal string, in the market currency
  currency: Currency;
  source: "yahoo" | "brapi";
  fetchedAt: string;    // ISO 8601
};

export type PriceProvider = {
  name: Quote["source"];
  supports: (market: Market) => boolean;
  getQuotes: (items: { ticker: string; market: Market }[]) => Promise<Quote[]>;
};
```
* Provider order: B3 → `[yahoo, brapi]`, US → `[yahoo]`.
* Symbol mapping lives inside each provider (`PETR4` + B3 → `PETR4.SA` for Yahoo; `PETR4` for brapi).
* Batch requests: one call per provider per refresh, not one per ticker.

### Profit formulas (`packages/shared/src/money/profit.ts`)
* `invested = quantity × averagePrice`
* `currentValue = quantity × currentPrice`
* `profit = currentValue − invested`
* `profitPercent = profit ÷ invested × 100` (0 when invested is 0)
* Portfolio totals: convert each position to the target currency (`USD × rate = BRL`, `BRL ÷ rate = USD`), then sum.
* Note: BRL totals of US positions use the **current** rate for both invested and current value (FX gain/loss is not tracked in this phase).

### API contract (additions)
| Method | Route | Response |
|---|---|---|
| GET | `/portfolio` | `{ positions: PortfolioPosition[], totals: { BRL: Totals, USD: Totals } \| null, fx: FxRate \| null, warnings: string[] }` |
| GET | `/fx/usd-brl` | `FxRate` (`{ rate: string, source: "awesomeapi", fetchedAt: string }`), or `503 FX_UNAVAILABLE` when no rate was ever fetched |
| POST | `/portfolio/refresh` | Clears the cache and returns a fresh `/portfolio` payload |

`PortfolioPosition` = `Position` + `{ quote: Quote \| null, currentValue, profit, profitPercent }` (null fields when no quote).
`Totals` = `{ invested, currentValue, profit, profitPercent }`.

When AwesomeAPI fails, the last known rate is used (with a warning). `fx` and `totals` are `null` only if no rate was ever fetched; positions are still returned.

## 3. Executable Commands
Same as Phase 1, plus:
* **Provider tests only:** `npx vitest run apps/api/src/prices`
* **Manual smoke test:** `curl -s localhost:3001/api/portfolio | jq`

## 4. Project Structure
```
apps/api/src/
├── prices/
│   ├── types.ts
│   ├── yahoo.ts            # PriceProvider implementation
│   ├── brapi.ts            # PriceProvider implementation
│   ├── get-quotes.ts       # fallback chain + cache
│   └── cache.ts            # tiny TTL cache
├── fx/
│   └── usd-brl.ts
└── portfolio/
    └── build-portfolio.ts  # combines positions + quotes + fx (uses shared profit fns)
packages/shared/src/money/
├── profit.ts
└── convert.ts
apps/web/src/features/portfolio/
```

## 5. Code Style and Conventions
* Same as Phase 1.
* Providers return data or throw a typed `ProviderError`; the fallback chain catches it and tries the next provider.
* External responses are parsed with Zod before use — never trust provider JSON shapes.
* Profit/convert functions are pure and fully covered by tests (including zero, losses and fractional quantities).

```ts
export function profit(quantity: string, averagePrice: string, currentPrice: string) {
  const invested = new Decimal(quantity).times(averagePrice);
  const currentValue = new Decimal(quantity).times(currentPrice);
  const amount = currentValue.minus(invested);
  const percent = invested.isZero() ? new Decimal(0) : amount.div(invested).times(100);
  return { invested, currentValue, amount, percent };
}
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Show positive profit in green and negative in red, with a `+`/`−` sign (not color alone).
  * Show the quote timestamp and a note that prices may be delayed ~15 min.
  * Log provider failures with ticker and provider name.
* **Ask first:**
  * Before adding a provider that requires a paid plan or an API key.
  * Before persisting quotes to the database.
* **Never do:**
  * Never call external providers from the frontend — always through the API.
  * Never call real providers in tests.
  * Never sum BRL and USD values without conversion.
