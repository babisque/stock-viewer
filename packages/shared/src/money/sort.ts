import Decimal from "decimal.js";
import type { Currency } from "../schemas/market";
import { convert } from "./convert";

// Value used to sort a money amount among amounts in other currencies:
// the amount converted to `target`. Returns undefined when it can't be
// compared (no amount, or a different currency and no rate) so the table
// can put it last.
export function moneySortKey(
  amount: string | null,
  currency: Currency,
  target: Currency,
  usdBrlRate: string | null,
): string | undefined {
  if (amount === null) return undefined;
  if (currency === target) return amount;
  if (usdBrlRate === null) return undefined;
  return convert(amount, currency, target, usdBrlRate).toString();
}

// Compares two decimal strings exactly (never through JS numbers).
export function compareDecimals(a: string, b: string): number {
  return new Decimal(a).comparedTo(b);
}
