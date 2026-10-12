import Decimal from "decimal.js";

export function investedAmount(
  quantity: string,
  averagePrice: string,
): Decimal {
  return new Decimal(quantity).times(averagePrice);
}
