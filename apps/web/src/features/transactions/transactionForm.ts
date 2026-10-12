import {
  B3_WHOLE_QUANTITY_MESSAGE,
  CreateTransactionSchema,
  formatQuantity,
  isValidQuantityForMarket,
  normalizeDecimalInput,
  todayIsoDate,
  type Market,
  type TransactionType,
} from "@stock-viewer/shared";
import Decimal from "decimal.js";
import { z } from "zod";

// Raw values as typed in the form (decimals may use a comma).
export type TransactionFormValues = {
  type: TransactionType;
  date: string; // YYYY-MM-DD, from <input type="date">
  quantity: string;
  unitPrice: string;
  fees: string;
};

export function emptyTransactionValues(
  today: string = todayIsoDate(),
): TransactionFormValues {
  return { type: "BUY", date: today, quantity: "", unitPrice: "", fees: "0" };
}

export function normalizeTransactionValues(
  values: TransactionFormValues,
): TransactionFormValues {
  const fees = normalizeDecimalInput(values.fees);
  return {
    ...values,
    quantity: normalizeDecimalInput(values.quantity),
    unitPrice: normalizeDecimalInput(values.unitPrice),
    fees: fees === "" ? "0" : fees, // an empty fees field means no fees
  };
}

// The shared schema plus the checks that need the position: whole B3
// quantities, and never selling more than is held now. The API also checks
// the sell against the quantity held on its date.
export function transactionFormSchema(market: Market, available: string) {
  return z.preprocess(
    (values: TransactionFormValues) => normalizeTransactionValues(values),
    CreateTransactionSchema.superRefine((value, ctx) => {
      if (!isValidQuantityForMarket(value.quantity, market)) {
        ctx.addIssue({
          code: "custom",
          path: ["quantity"],
          message: B3_WHOLE_QUANTITY_MESSAGE,
        });
      } else if (
        value.type === "SELL" &&
        new Decimal(value.quantity).greaterThan(available)
      ) {
        ctx.addIssue({
          code: "custom",
          path: ["quantity"],
          message: `You can sell at most ${formatQuantity(available, market)}`,
        });
      }
    }),
  );
}

function parseDecimal(value: string): Decimal | null {
  try {
    return new Decimal(normalizeDecimalInput(value) || "0");
  } catch {
    return null;
  }
}

// Money that moved, in the position's currency: what a buy cost
// (price × quantity + fees) or what a sell brought in (minus fees).
// Null while the inputs aren't numbers yet.
export function transactionTotal(values: {
  type: TransactionType;
  quantity: string;
  unitPrice: string;
  fees: string;
}): Decimal | null {
  const quantity = parseDecimal(values.quantity);
  const unitPrice = parseDecimal(values.unitPrice);
  const fees = parseDecimal(values.fees);
  if (!quantity || !unitPrice || !fees) return null;
  const gross = quantity.times(unitPrice);
  return values.type === "BUY" ? gross.plus(fees) : gross.minus(fees);
}
