import {
  currencyForMarket,
  derivePosition,
  isInsufficientQuantityError,
  type DerivedPosition,
  type Transaction,
} from "@stock-viewer/shared";
import type {
  Market,
  Position as PositionRow,
  Prisma,
  Transaction as TransactionRow,
} from "../generated/prisma/client";
import { appError } from "../lib/errors";

// Helpers shared by the positions and transactions modules: a position's
// quantity, average price and realized profit always come from its
// transactions.

export type PositionWithTransactions = PositionRow & {
  transactions: TransactionRow[];
};

// Trade dates are stored as UTC midnight so the calendar day never shifts.
export function isoDateToDate(date: string): Date {
  return new Date(`${date}T00:00:00.000Z`);
}

export function toTransactionDto(
  row: TransactionRow,
  market: Market,
): Transaction {
  return {
    id: row.id,
    positionId: row.positionId,
    type: row.type,
    date: row.date.toISOString().slice(0, 10),
    quantity: row.quantity.toFixed(),
    unitPrice: row.unitPrice.toFixed(),
    fees: row.fees.toFixed(),
    currency: currencyForMarket(market),
    createdAt: row.createdAt.toISOString(),
  };
}

// Throws a 409 app error if the history sells more than it holds.
export function deriveFromRow(row: PositionWithTransactions): DerivedPosition {
  try {
    return derivePosition(
      row.transactions.map((transaction) =>
        toTransactionDto(transaction, row.market),
      ),
    );
  } catch (error) {
    if (isInsufficientQuantityError(error)) {
      throw appError(409, "INSUFFICIENT_QUANTITY", error.message);
    }
    throw error;
  }
}

// Re-derives a position after its transactions changed and mirrors quantity
// and average price into the Position row. Run it inside the same database
// transaction as the change, so an invalid history is rolled back.
export async function syncPosition(
  tx: Prisma.TransactionClient,
  positionId: string,
): Promise<PositionWithTransactions> {
  const row = await tx.position.findUniqueOrThrow({
    where: { id: positionId },
    include: { transactions: true },
  });
  const derived = deriveFromRow(row);
  return tx.position.update({
    where: { id: positionId },
    data: {
      quantity: derived.quantity.toFixed(),
      averagePrice: derived.averagePrice.toFixed(),
    },
    include: { transactions: true },
  });
}
