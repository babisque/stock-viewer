import {
  B3_WHOLE_QUANTITY_MESSAGE,
  isValidQuantityForMarket,
  sortTransactions,
  type CreateTransaction,
  type Position,
  type Transaction,
} from "@stock-viewer/shared";
import { appError, isAppError } from "../lib/errors";
import type { PrismaClient } from "../lib/prisma";
import { positionNotFound, toPositionDto } from "../positions/positions";
import { isoDateToDate, syncPosition, toTransactionDto } from "./ledger";

function transactionNotFound(id: string) {
  return appError(404, "TRANSACTION_NOT_FOUND", `Transaction ${id} not found`);
}

// In the order they are applied: by date, then by when they were recorded.
export async function listTransactions(
  prisma: PrismaClient,
  positionId: string,
): Promise<Transaction[]> {
  const position = await prisma.position.findUnique({
    where: { id: positionId },
    include: { transactions: true },
  });
  if (!position) throw positionNotFound(positionId);
  return sortTransactions(
    position.transactions.map((row) => toTransactionDto(row, position.market)),
  );
}

// Records a buy or sell and returns the position with its new derived
// quantity and average price. A sell larger than the quantity held at its
// date is rejected with 409 and nothing is saved.
export async function createTransaction(
  prisma: PrismaClient,
  positionId: string,
  input: CreateTransaction,
): Promise<Position> {
  return prisma.$transaction(async (tx) => {
    const position = await tx.position.findUnique({
      where: { id: positionId },
    });
    if (!position) throw positionNotFound(positionId);

    if (!isValidQuantityForMarket(input.quantity, position.market)) {
      throw appError(
        400,
        "VALIDATION_ERROR",
        `quantity: ${B3_WHOLE_QUANTITY_MESSAGE}`,
      );
    }

    await tx.transaction.create({
      data: {
        positionId,
        type: input.type,
        date: isoDateToDate(input.date),
        quantity: input.quantity,
        unitPrice: input.unitPrice,
        fees: input.fees,
      },
    });
    return toPositionDto(await syncPosition(tx, positionId));
  });
}

// Deleting a buy that a later sell depends on is rejected with 409.
export async function deleteTransaction(
  prisma: PrismaClient,
  id: string,
): Promise<void> {
  await prisma.$transaction(async (tx) => {
    const existing = await tx.transaction.findUnique({ where: { id } });
    if (!existing) throw transactionNotFound(id);

    await tx.transaction.delete({ where: { id } });
    try {
      await syncPosition(tx, existing.positionId);
    } catch (error) {
      if (isAppError(error) && error.code === "INSUFFICIENT_QUANTITY") {
        throw appError(
          409,
          "INSUFFICIENT_QUANTITY",
          `Can't delete this transaction. ${error.message}`,
        );
      }
      throw error;
    }
  });
}
