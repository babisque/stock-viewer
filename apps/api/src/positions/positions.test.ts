import { todayIsoDate } from "@stock-viewer/shared";
import { describe, expect, it } from "vitest";
import { setupTestContext } from "../../test/helpers";
import {
  comparePositions,
  createPosition,
  deletePosition,
  listLedgerPositions,
  listPositions,
  updatePosition,
} from "./positions";

const { prisma } = setupTestContext();

const petr4 = {
  ticker: "PETR4",
  market: "B3",
  quantity: "100",
  averagePrice: "28.45",
  notes: null,
} as const;

describe("positions domain", () => {
  it("creates a position and returns it as a DTO with string decimals and currency", async () => {
    const position = await createPosition(prisma, petr4);
    expect(position).toMatchObject({
      ticker: "PETR4",
      market: "B3",
      currency: "BRL",
      quantity: "100",
      averagePrice: "28.45",
      notes: null,
    });
    expect(typeof position.id).toBe("string");
    expect(new Date(position.createdAt).toISOString()).toBe(position.createdAt);
  });

  it("rejects a duplicate ticker+market with a 409 app error", async () => {
    await createPosition(prisma, petr4);
    await expect(createPosition(prisma, petr4)).rejects.toMatchObject({
      statusCode: 409,
      code: "DUPLICATE_POSITION",
    });
  });

  it("allows the same ticker on different markets", async () => {
    await createPosition(prisma, petr4);
    await expect(
      createPosition(prisma, { ...petr4, market: "US" }),
    ).resolves.toMatchObject({
      currency: "USD",
    });
  });

  it("lists positions sorted by market then ticker", async () => {
    await createPosition(prisma, { ...petr4, ticker: "AAPL", market: "US" });
    await createPosition(prisma, { ...petr4, ticker: "VALE3" });
    await createPosition(prisma, petr4);
    const tickers = (await listPositions(prisma)).map(
      (p) => `${p.market}:${p.ticker}`,
    );
    expect(tickers).toEqual(["B3:PETR4", "B3:VALE3", "US:AAPL"]);
  });

  it("records the entered values as an initial BUY dated today", async () => {
    const created = await createPosition(prisma, petr4);
    const rows = await prisma.transaction.findMany({
      where: { positionId: created.id },
    });
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({ type: "BUY" });
    expect(rows[0]?.quantity.toString()).toBe("100");
    expect(rows[0]?.unitPrice.toString()).toBe("28.45");
    expect(rows[0]?.fees.toString()).toBe("0");
    expect(rows[0]?.date.toISOString().slice(0, 10)).toBe(todayIsoDate());
  });

  it("updates the notes", async () => {
    const created = await createPosition(prisma, petr4);
    const updated = await updatePosition(prisma, created.id, {
      notes: "long term",
    });
    expect(updated).toMatchObject({
      ticker: "PETR4",
      quantity: "100",
      averagePrice: "28.45",
      notes: "long term",
    });
  });

  it("accepts quantity and average price sent back unchanged", async () => {
    const created = await createPosition(prisma, petr4);
    await expect(
      updatePosition(prisma, created.id, {
        quantity: "100.0",
        averagePrice: "28.450",
        notes: null,
      }),
    ).resolves.toMatchObject({ quantity: "100", averagePrice: "28.45" });
  });

  it.each([
    [{ quantity: "150" }, "Quantity is calculated from the transactions"],
    [
      { averagePrice: "30" },
      "Average price is calculated from the transactions",
    ],
  ])("rejects a changed derived value %j", async (change, message) => {
    const created = await createPosition(prisma, petr4);
    await expect(
      updatePosition(prisma, created.id, { ...change, notes: null }),
    ).rejects.toMatchObject({
      statusCode: 409,
      code: "DERIVED_VALUE_READ_ONLY",
      message: expect.stringContaining(message),
    });
  });

  it("derives quantity and average price from the transactions", async () => {
    const created = await createPosition(prisma, petr4);
    await prisma.transaction.create({
      data: {
        positionId: created.id,
        type: "BUY",
        date: new Date("2026-01-01T00:00:00.000Z"),
        quantity: "300",
        unitPrice: "10",
        fees: "1",
      },
    });
    // (100 × 28.45 + 300 × 10 + 1) ÷ 400; the mirrored column is not read.
    const [position] = await listLedgerPositions(prisma);
    expect(position).toMatchObject({
      quantity: "400",
      averagePrice: "14.615",
      realizedProfit: "0",
    });
  });

  it("returns 404 errors for unknown ids", async () => {
    const input = { notes: null };
    await expect(
      updatePosition(prisma, "missing", input),
    ).rejects.toMatchObject({
      statusCode: 404,
      code: "POSITION_NOT_FOUND",
    });
    await expect(deletePosition(prisma, "missing")).rejects.toMatchObject({
      statusCode: 404,
    });
  });

  it("deletes a position together with its transactions", async () => {
    const created = await createPosition(prisma, petr4);
    await deletePosition(prisma, created.id);
    expect(await listPositions(prisma)).toEqual([]);
    expect(await prisma.transaction.count()).toBe(0);
  });
});

describe("comparePositions", () => {
  it("orders B3 before US", () => {
    const base = {
      ...petr4,
      id: "",
      currency: "BRL",
      createdAt: "",
      updatedAt: "",
    } as const;
    expect(
      comparePositions(
        { ...base, market: "US", ticker: "A" },
        { ...base, ticker: "Z" },
      ),
    ).toBeGreaterThan(0);
  });
});
