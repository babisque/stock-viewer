import type { QuoteSource } from "@stock-viewer/shared";
import Decimal from "decimal.js";

const SOURCE_LABELS: Record<QuoteSource | "awesomeapi", string> = {
  yahoo: "Yahoo",
  brapi: "brapi",
  awesomeapi: "AwesomeAPI",
};

const timeFormatter = new Intl.DateTimeFormat("pt-BR", {
  hour: "2-digit",
  minute: "2-digit",
});

// "14:32" (local time)
export function formatTime(date: Date | string | number): string {
  return timeFormatter.format(new Date(date));
}

// "Yahoo · 14:32" (local time)
export function sourceAndTime(
  source: QuoteSource | "awesomeapi",
  fetchedAt: string,
): string {
  return `${SOURCE_LABELS[source]} · ${formatTime(fetchedAt)}`;
}

// Colour for a signed value; the sign itself comes from the formatters so the
// meaning never depends on colour alone.
export function signClass(value: string): string {
  const decimal = new Decimal(value).toDecimalPlaces(2);
  if (decimal.isPositive() && !decimal.isZero()) return "text-gain";
  if (decimal.isNegative() && !decimal.isZero()) return "text-loss";
  return "";
}

const tradeDateFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  timeZone: "UTC",
});

// "2026-10-12" -> "12/10/2026". Trade dates have no time, so format in UTC
// to keep the same calendar day in every time zone.
export function formatTradeDate(date: string): string {
  return tradeDateFormatter.format(new Date(`${date}T00:00:00.000Z`));
}
