import { z } from "zod";
import { CurrencySchema, MarketSchema } from "./market";
import { PositionSchema } from "./position";

// All money values are decimal strings at full precision; the UI rounds them.

export const QuoteSourceSchema = z.enum(["yahoo", "brapi"]);
export type QuoteSource = z.infer<typeof QuoteSourceSchema>;

export const QuoteSchema = z.object({
  ticker: z.string(),
  market: MarketSchema,
  price: z.string(), // in the market currency
  currency: CurrencySchema,
  source: QuoteSourceSchema,
  fetchedAt: z.string(), // ISO 8601
});
export type Quote = z.infer<typeof QuoteSchema>;

export const FxRateSchema = z.object({
  rate: z.string(), // BRL per 1 USD
  source: z.literal("awesomeapi"),
  fetchedAt: z.string(),
});
export type FxRate = z.infer<typeof FxRateSchema>;

export const TotalsSchema = z.object({
  invested: z.string(),
  currentValue: z.string(),
  profit: z.string(),
  profitPercent: z.string(),
  realizedProfit: z.string(),
});
export type Totals = z.infer<typeof TotalsSchema>;

// Value fields are null when no provider could price the position, or when
// it is closed (quantity 0). `profit` is unrealized; `realizedProfit` comes
// from the sells and is always known.
export const PortfolioPositionSchema = PositionSchema.extend({
  quote: QuoteSchema.nullable(),
  currentValue: z.string().nullable(),
  profit: z.string().nullable(),
  profitPercent: z.string().nullable(),
  realizedProfit: z.string(),
});
export type PortfolioPosition = z.infer<typeof PortfolioPositionSchema>;

// `fx` and `totals` are null only when the USD/BRL rate has never been
// fetched successfully (totals can't be built without converting).
export const PortfolioSchema = z.object({
  positions: z.array(PortfolioPositionSchema),
  totals: z.object({ BRL: TotalsSchema, USD: TotalsSchema }).nullable(),
  fx: FxRateSchema.nullable(),
  warnings: z.array(z.string()),
});
export type Portfolio = z.infer<typeof PortfolioSchema>;
