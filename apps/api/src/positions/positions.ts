import {
  currencyForMarket,
  todayIsoDate,
  type CreatePosition,
  type DerivedPosition,
  type Position,
  type UpdatePosition,
} from "@stock-viewer/shared";
import Decimal from "decimal.js";
import type { PrismaClient } from "../lib/prisma";
import { appError, hasPrismaCode } from "../lib/errors";
import {
  deriveFromRow,
  isoDateToDate,
  type PositionWithTransactions,
} from "../transactions/ledger";

const MARKET_ORDER = { B3: 0, US: 1 } as const;

// A position plus the profit already realized by its sells, in its currency.
export type LedgerPosition = Position & { realizedProfit: string };

// Quantity and average price come from the transactions, not the mirrored
// columns: SQLite stores those as floats.
function toDto(
  row: PositionWithTransactions,
  derived: DerivedPosition,
): Position {
  return {
    id: row.id,
    ticker: row.ticker,
    market: row.market,
    currency: currencyForMarket(row.market),
    quantity: derived.quantity.toFixed(),
    averagePrice: derived.averagePrice.toFixed(),
    notes: row.notes,
    createdAt: row.createdAt.toISOString(),
    updatedAt: row.updatedAt.toISOString(),
  };
}

export function toPositionDto(row: PositionWithTransactions): Position {
  return toDto(row, deriveFromRow(row));
}

export function toLedgerPosition(
  row: PositionWithTransactions,
): LedgerPosition {
  const derived = deriveFromRow(row);
  return {
    ...toDto(row, derived),
    realizedProfit: derived.realizedProfit.toFixed(),
  };
}

export function comparePositions(a: Position, b: Position): number {
  return (
    MARKET_ORDER[a.market] - MARKET_ORDER[b.market] ||
    a.ticker.localeCompare(b.ticker)
  );
}

export function positionNotFound(id: string) {
  return appError(404, "POSITION_NOT_FOUND", `Position ${id} not found`);
}

const withTransactions = { transactions: true } as const;

export async function listLedgerPositions(
  prisma: PrismaClient,
): Promise<LedgerPosition[]> {
  const rows = await prisma.position.findMany({ include: withTransactions });
  return rows.map(toLedgerPosition).sort(comparePositions);
}

export async function listPositions(prisma: PrismaClient): Promise<Position[]> {
  const rows = await prisma.position.findMany({ include: withTransactions });
  return rows.map(toPositionDto).sort(comparePositions);
}

// The entered quantity and average price become the position's first BUY,
// dated today, without fees.
export async function createPosition(
  prisma: PrismaClient,
  input: CreatePosition,
): Promise<Position> {
  try {
    const row = await prisma.position.create({
      data: {
        ...input,
        transactions: {
          create: {
            type: "BUY",
            date: isoDateToDate(todayIsoDate()),
            quantity: input.quantity,
            unitPrice: input.averagePrice,
          },
        },
      },
      include: withTransactions,
    });
    return toPositionDto(row);
  } catch (error) {
    if (hasPrismaCode(error, "P2002")) {
      throw appError(
        409,
        "DUPLICATE_POSITION",
        `A position for ${input.ticker} (${input.market}) already exists`,
      );
    }
    throw error;
  }
}

function derivedValueReadOnly(label: string) {
  return appError(
    409,
    "DERIVED_VALUE_READ_ONLY",
    `${label} is calculated from the transactions; add a buy or sell instead`,
  );
}

// Only the notes can change. Quantity and average price may be sent back
// unchanged, but any other value is rejected.
export async function updatePosition(
  prisma: PrismaClient,
  id: string,
  input: UpdatePosition,
): Promise<Position> {
  const existing = await prisma.position.findUnique({
    where: { id },
    include: withTransactions,
  });
  if (!existing) throw positionNotFound(id);

  const current = toPositionDto(existing);
  if (
    input.quantity !== undefined &&
    !new Decimal(input.quantity).equals(current.quantity)
  ) {
    throw derivedValueReadOnly("Quantity");
  }
  if (
    input.averagePrice !== undefined &&
    !new Decimal(input.averagePrice).equals(current.averagePrice)
  ) {
    throw derivedValueReadOnly("Average price");
  }

  const row = await prisma.position.update({
    where: { id },
    data: { notes: input.notes },
    include: withTransactions,
  });
  return toPositionDto(row);
}

export async function deletePosition(
  prisma: PrismaClient,
  id: string,
): Promise<void> {
  try {
    await prisma.position.delete({ where: { id } });
  } catch (error) {
    if (hasPrismaCode(error, "P2025")) throw positionNotFound(id);
    throw error;
  }
}
