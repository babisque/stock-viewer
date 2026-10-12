import Decimal from "decimal.js";
import type { Currency, Market } from "../schemas/market";

const LOCALE_BY_CURRENCY: Record<Currency, string> = {
  BRL: "pt-BR",
  USD: "en-US",
};
const LOCALE_BY_MARKET: Record<Market, string> = { B3: "pt-BR", US: "en-US" };

// Rounding happens here, and only here: values are rounded to cents for display.
// Intl accepts decimal strings, so no precision is lost before rounding.
export function formatMoney(
  amount: Decimal | string,
  currency: Currency,
): string {
  const rounded = new Decimal(amount)
    .toDecimalPlaces(2, Decimal.ROUND_HALF_UP)
    .toFixed(2);
  const formatter = new Intl.NumberFormat(LOCALE_BY_CURRENCY[currency], {
    style: "currency",
    currency,
  });
  return formatter.format(rounded as Intl.StringNumericLiteral);
}

export function formatQuantity(
  quantity: Decimal | string,
  market: Market,
): string {
  const formatter = new Intl.NumberFormat(LOCALE_BY_MARKET[market], {
    maximumFractionDigits: 8,
  });
  return formatter.format(
    new Decimal(quantity).toFixed() as Intl.StringNumericLiteral,
  );
}

// Accepts "28,45" (Brazilian style) as well as "28.45" and returns "28.45".
export function normalizeDecimalInput(value: string): string {
  return value.trim().replace(",", ".");
}

const MINUS = "−"; // typographic minus sign, easier to read than "-"

function signOf(value: Decimal): string {
  if (value.isZero()) return "";
  return value.isNegative() ? MINUS : "+";
}

// "+R$ 10,00" / "−$5.00": the sign is always shown so profit is not
// conveyed by colour alone.
export function formatSignedMoney(
  amount: Decimal | string,
  currency: Currency,
): string {
  const value = new Decimal(amount).toDecimalPlaces(2, Decimal.ROUND_HALF_UP);
  return `${signOf(value)}${formatMoney(value.abs(), currency)}`;
}

// "+12.34%" / "−5.00%"
export function formatPercent(value: Decimal | string): string {
  const rounded = new Decimal(value).toDecimalPlaces(2, Decimal.ROUND_HALF_UP);
  return `${signOf(rounded)}${rounded.abs().toFixed(2)}%`;
}

// "12.34%" without a sign: a share of a whole (e.g. allocation).
export function formatShare(value: Decimal | string): string {
  return `${new Decimal(value).toDecimalPlaces(2, Decimal.ROUND_HALF_UP).toFixed(2)}%`;
}
