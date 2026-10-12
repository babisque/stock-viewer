import { describe, expect, it } from "vitest";
import {
  derivePosition,
  isClosedPosition,
  isInsufficientQuantityError,
  sortTransactions,
  type LedgerTransaction,
} from "./average-price";

let nextId = 0;

// Compact transaction builder: tx("BUY", "2026-01-10", "100", "10", "5").
function tx(
  type: LedgerTransaction["type"],
  date: string,
  quantity: string,
  unitPrice: string,
  fees = "0",
  createdAt = "2026-10-01T12:00:00.000Z",
): LedgerTransaction {
  nextId += 1;
  return {
    id: `tx${String(nextId).padStart(4, "0")}`,
    type,
    date,
    createdAt,
    quantity,
    unitPrice,
    fees,
  };
}

function derived(transactions: LedgerTransaction[]) {
  const result = derivePosition(transactions);
  return {
    quantity: result.quantity.toString(),
    averagePrice: result.averagePrice.toString(),
    realizedProfit: result.realizedProfit.toString(),
  };
}

describe("derivePosition", () => {
  it.each([
    {
      name: "no transactions",
      transactions: [],
      expected: { quantity: "0", averagePrice: "0", realizedProfit: "0" },
    },
    {
      name: "a single buy",
      transactions: [tx("BUY", "2026-01-10", "100", "28.45")],
      expected: { quantity: "100", averagePrice: "28.45", realizedProfit: "0" },
    },
    {
      name: "multiple buys average by quantity",
      transactions: [
        tx("BUY", "2026-01-10", "100", "10"),
        tx("BUY", "2026-02-10", "300", "14"),
      ],
      // (100 × 10 + 300 × 14) ÷ 400 = 5200 ÷ 400
      expected: { quantity: "400", averagePrice: "13", realizedProfit: "0" },
    },
    {
      name: "buy fees go into the average price",
      transactions: [tx("BUY", "2026-01-10", "100", "10", "5")],
      expected: { quantity: "100", averagePrice: "10.05", realizedProfit: "0" },
    },
    {
      name: "buys with fees on both",
      transactions: [
        tx("BUY", "2026-01-10", "100", "10", "5"),
        tx("BUY", "2026-02-10", "100", "20", "5"),
      ],
      // (1005 + 2000 + 5) ÷ 200
      expected: { quantity: "200", averagePrice: "15.05", realizedProfit: "0" },
    },
    {
      name: "a partial sell keeps the average and realizes profit",
      transactions: [
        tx("BUY", "2026-01-10", "100", "10"),
        tx("SELL", "2026-02-10", "40", "15"),
      ],
      expected: { quantity: "60", averagePrice: "10", realizedProfit: "200" },
    },
    {
      name: "sell fees reduce realized profit",
      transactions: [
        tx("BUY", "2026-01-10", "100", "10"),
        tx("SELL", "2026-02-10", "40", "15", "4.90"),
      ],
      expected: {
        quantity: "60",
        averagePrice: "10",
        realizedProfit: "195.1",
      },
    },
    {
      name: "a sell below the average realizes a loss",
      transactions: [
        tx("BUY", "2026-01-10", "10", "20"),
        tx("SELL", "2026-02-10", "10", "15", "1"),
      ],
      expected: { quantity: "0", averagePrice: "20", realizedProfit: "-51" },
    },
    {
      name: "full sell then rebuy starts a new average",
      transactions: [
        tx("BUY", "2026-01-10", "100", "10"),
        tx("SELL", "2026-02-10", "100", "12"),
        tx("BUY", "2026-03-10", "50", "30", "2"),
      ],
      // Realized profit from the first round is kept.
      expected: {
        quantity: "50",
        averagePrice: "30.04",
        realizedProfit: "200",
      },
    },
    {
      name: "fractional US quantities without float errors",
      transactions: [
        tx("BUY", "2026-01-10", "0.1", "0.2"),
        tx("BUY", "2026-01-11", "0.2", "0.1"),
        tx("SELL", "2026-01-12", "0.3", "0.3"),
      ],
      // avg = (0.02 + 0.02) ÷ 0.3 repeats, so decimal.js keeps it at 20
      // significant digits; the 1e-21 left in the profit disappears when
      // rounding to cents for display.
      expected: {
        quantity: "0",
        averagePrice: "0.13333333333333333333",
        realizedProfit: "0.050000000000000000001",
      },
    },
  ])("$name", ({ transactions, expected }) => {
    expect(derived(transactions)).toEqual(expected);
  });

  it("processes transactions in date order, not input order", () => {
    const transactions = [
      tx("SELL", "2026-03-10", "50", "20"),
      tx("BUY", "2026-01-10", "100", "10"),
    ];
    expect(derived(transactions)).toEqual({
      quantity: "50",
      averagePrice: "10",
      realizedProfit: "500",
    });
  });

  it("breaks same-date ties by createdAt", () => {
    const sell = tx(
      "SELL",
      "2026-01-10",
      "100",
      "12",
      "0",
      "2026-01-10T15:00:00.000Z",
    );
    const buy = tx(
      "BUY",
      "2026-01-10",
      "100",
      "10",
      "0",
      "2026-01-10T14:00:00.000Z",
    );
    expect(derived([sell, buy])).toMatchObject({
      quantity: "0",
      realizedProfit: "200",
    });
  });

  it("rejects a sell larger than the quantity held", () => {
    const sell = tx("SELL", "2026-02-10", "101", "10");
    let caught: unknown;
    try {
      derivePosition([tx("BUY", "2026-01-10", "100", "10"), sell]);
    } catch (error) {
      caught = error;
    }
    expect(isInsufficientQuantityError(caught)).toBe(true);
    if (isInsufficientQuantityError(caught)) {
      expect(caught.transaction).toBe(sell);
      expect(caught.available.toString()).toBe("100");
    }
  });

  it("rejects a backdated sell even when today's quantity would cover it", () => {
    const transactions = [
      tx("BUY", "2026-01-10", "100", "10"),
      tx("SELL", "2026-01-05", "50", "10"),
    ];
    expect(() => derivePosition(transactions)).toThrow(
      "Can't sell 50 on 2026-01-05: only 0 held at that point",
    );
  });
});

describe("sortTransactions", () => {
  it("does not mutate its input", () => {
    const transactions = [
      tx("BUY", "2026-03-10", "1", "1"),
      tx("BUY", "2026-01-10", "1", "1"),
    ];
    const copy = [...transactions];
    expect(sortTransactions(transactions).map((t) => t.date)).toEqual([
      "2026-01-10",
      "2026-03-10",
    ]);
    expect(transactions).toEqual(copy);
  });
});

describe("isClosedPosition", () => {
  it("is true only for quantity 0", () => {
    expect(isClosedPosition({ quantity: "0" })).toBe(true);
    expect(isClosedPosition({ quantity: "0.00000001" })).toBe(false);
  });
});
