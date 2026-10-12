import Decimal from "decimal.js";
import { describe, expect, it } from "vitest";
import { deriveFromRow, isoDateToDate, toTransactionDto } from "./ledger";

const createdAt = new Date("2026-10-12T13:00:00.000Z");

function row(overrides: object = {}) {
  return {
    id: "tx-1",
    positionId: "pos-1",
    type: "BUY" as const,
    date: isoDateToDate("2026-03-01"),
    quantity: new Decimal("0.00000001"),
    unitPrice: new Decimal("190.5"),
    fees: new Decimal("0"),
    createdAt,
    ...overrides,
  };
}

describe("isoDateToDate", () => {
  it("stores the calendar day at UTC midnight", () => {
    expect(isoDateToDate("2026-03-01").toISOString()).toBe(
      "2026-03-01T00:00:00.000Z",
    );
  });
});

describe("toTransactionDto", () => {
  it("returns plain decimal strings, the trade date and the currency", () => {
    expect(toTransactionDto(row(), "US")).toEqual({
      id: "tx-1",
      positionId: "pos-1",
      type: "BUY",
      date: "2026-03-01",
      quantity: "0.00000001", // not "1e-8"
      unitPrice: "190.5",
      fees: "0",
      currency: "USD",
      createdAt: "2026-10-12T13:00:00.000Z",
    });
  });
});

describe("deriveFromRow", () => {
  const position = {
    id: "pos-1",
    ticker: "PETR4",
    market: "B3" as const,
    quantity: new Decimal(0),
    averagePrice: new Decimal(0),
    notes: null,
    createdAt,
    updatedAt: createdAt,
  };

  it("turns an oversell into a 409 app error", () => {
    expect(() =>
      deriveFromRow({
        ...position,
        transactions: [row({ type: "SELL", quantity: new Decimal(1) })],
      }),
    ).toThrow(
      expect.objectContaining({
        statusCode: 409,
        code: "INSUFFICIENT_QUANTITY",
      }),
    );
  });
});
