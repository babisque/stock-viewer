import Decimal from "decimal.js";
import type { Currency } from "../schemas/market";
import { convert } from "./convert";
import { investedAmount } from "./invested";

function percentOf(amount: Decimal, base: Decimal): Decimal {
  return base.isZero() ? new Decimal(0) : amount.div(base).times(100);
}

// All values are in the position's own (market) currency.
export function profit(
  quantity: string,
  averagePrice: string,
  currentPrice: string,
) {
  const invested = investedAmount(quantity, averagePrice);
  const currentValue = new Decimal(quantity).times(currentPrice);
  const amount = currentValue.minus(invested);
  const percent = percentOf(amount, invested);
  return { invested, currentValue, amount, percent };
}

export type TotalsItem = {
  currency: Currency;
  invested: Decimal | string;
  currentValue: Decimal | string;
};

// Converts every item to the target currency with the *current* rate, then
// sums. FX gain/loss on US positions is not tracked yet.
export function portfolioTotals(
  items: TotalsItem[],
  target: Currency,
  usdBrlRate: Decimal | string,
) {
  let invested = new Decimal(0);
  let currentValue = new Decimal(0);
  for (const item of items) {
    invested = invested.plus(
      convert(item.invested, item.currency, target, usdBrlRate),
    );
    currentValue = currentValue.plus(
      convert(item.currentValue, item.currency, target, usdBrlRate),
    );
  }
  const amount = currentValue.minus(invested);
  return {
    invested,
    currentValue,
    profit: amount,
    profitPercent: percentOf(amount, invested),
  };
}

export type RealizedItem = {
  currency: Currency;
  realizedProfit: Decimal | string;
};

// Realized profit doesn't depend on a quote, so every position counts here,
// priced or not. Converted with the *current* rate, like the other totals.
export function realizedProfitTotal(
  items: RealizedItem[],
  target: Currency,
  usdBrlRate: Decimal | string,
): Decimal {
  return items.reduce(
    (sum, item) =>
      sum.plus(convert(item.realizedProfit, item.currency, target, usdBrlRate)),
    new Decimal(0),
  );
}
