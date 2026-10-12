import { z } from "zod";

export const MarketSchema = z.enum(["B3", "US"]);
export type Market = z.infer<typeof MarketSchema>;

export const CurrencySchema = z.enum(["BRL", "USD"]);
export type Currency = z.infer<typeof CurrencySchema>;

export function currencyForMarket(market: Market): Currency {
  return market === "B3" ? "BRL" : "USD";
}
