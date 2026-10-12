import Decimal from "decimal.js";
import type { Transaction } from "../schemas/transaction";

// Only the fields the derivation needs, so callers can pass DTOs or rows
// mapped to strings. All amounts are in the position's own currency.
export type LedgerTransaction = Pick<
  Transaction,
  "id" | "type" | "date" | "createdAt" | "quantity" | "unitPrice" | "fees"
>;

export type DerivedPosition = {
  quantity: Decimal;
  averagePrice: Decimal;
  realizedProfit: Decimal;
};

export type InsufficientQuantityError = Error & {
  transaction: LedgerTransaction;
  available: Decimal;
};

function insufficientQuantityError(
  transaction: LedgerTransaction,
  available: Decimal,
): InsufficientQuantityError {
  return Object.assign(
    new Error(
      `Can't sell ${transaction.quantity} on ${transaction.date}: only ${available.toString()} held at that point`,
    ),
    { transaction, available, isInsufficientQuantityError: true },
  );
}

export function isInsufficientQuantityError(
  error: unknown,
): error is InsufficientQuantityError {
  return (
    error instanceof Error &&
    "isInsufficientQuantityError" in error &&
    error.isInsufficientQuantityError === true
  );
}

export function emptyPosition(): DerivedPosition {
  return {
    quantity: new Decimal(0),
    averagePrice: new Decimal(0),
    realizedProfit: new Decimal(0),
  };
}

// By trade date, then by when it was recorded; the id breaks exact ties so
// the order is always the same.
export function compareTransactions(
  a: LedgerTransaction,
  b: LedgerTransaction,
): number {
  if (a.date !== b.date) return a.date < b.date ? -1 : 1;
  if (a.createdAt !== b.createdAt) return a.createdAt < b.createdAt ? -1 : 1;
  if (a.id !== b.id) return a.id < b.id ? -1 : 1;
  return 0;
}

export function sortTransactions<T extends LedgerTransaction>(
  transactions: T[],
): T[] {
  return [...transactions].sort(compareTransactions);
}

// Brazilian average-cost method:
// - BUY: fees go into the cost; when the position was empty, `qty × avg` is 0,
//   so the average starts over from this buy.
// - SELL: the average is unchanged; profit = (price − avg) × qty − fees.
export function applyTransaction(
  position: DerivedPosition,
  transaction: LedgerTransaction,
): DerivedPosition {
  const quantity = new Decimal(transaction.quantity);
  const unitPrice = new Decimal(transaction.unitPrice);
  const fees = new Decimal(transaction.fees);

  if (transaction.type === "BUY") {
    const newQuantity = position.quantity.plus(quantity);
    const totalCost = position.quantity
      .times(position.averagePrice)
      .plus(quantity.times(unitPrice))
      .plus(fees);
    return {
      quantity: newQuantity,
      averagePrice: totalCost.div(newQuantity),
      realizedProfit: position.realizedProfit,
    };
  }

  if (quantity.greaterThan(position.quantity)) {
    throw insufficientQuantityError(transaction, position.quantity);
  }
  const profit = unitPrice
    .minus(position.averagePrice)
    .times(quantity)
    .minus(fees);
  return {
    quantity: position.quantity.minus(quantity),
    averagePrice: position.averagePrice,
    realizedProfit: position.realizedProfit.plus(profit),
  };
}

// Replays the whole history in order. Throws an InsufficientQuantityError if
// any sell exceeds the quantity held at that point.
export function derivePosition(
  transactions: LedgerTransaction[],
): DerivedPosition {
  return sortTransactions(transactions).reduce(
    applyTransaction,
    emptyPosition(),
  );
}

// A position whose shares were all sold. It stays in the portfolio because it
// still carries realized profit.
export function isClosedPosition(position: { quantity: string }): boolean {
  return new Decimal(position.quantity).isZero();
}
