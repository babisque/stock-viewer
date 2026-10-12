import Decimal from "decimal.js";
import type { Currency } from "../schemas/market";

// The USD/BRL rate is how many BRL one USD buys (e.g. "5.42").
// This is the only place where amounts move between currencies.
export function convert(
  amount: Decimal | string,
  from: Currency,
  to: Currency,
  usdBrlRate: Decimal | string,
): Decimal {
  const rate = new Decimal(usdBrlRate);
  if (!rate.greaterThan(0)) {
    throw new Error(`Invalid USD/BRL rate: ${rate.toString()}`);
  }
  const value = new Decimal(amount);
  if (from === to) return value;
  return from === "USD" ? value.times(rate) : value.div(rate);
}
