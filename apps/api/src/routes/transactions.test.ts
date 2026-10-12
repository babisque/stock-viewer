import { todayIsoDate } from "@stock-viewer/shared";
import { describe, expect, it } from "vitest";
import { setupTestContext } from "../../test/helpers";

const { app, prisma } = setupTestContext();

const today = todayIsoDate();

// Creating a position records an initial BUY of 100 @ 10, dated today.
async function createPosition(market: "B3" | "US" = "B3") {
  const response = await app.inject({
    method: "POST",
    url: "/api/positions",
    payload: {
      ticker: market === "B3" ? "PETR4" : "AAPL",
      market,
      quantity: "100",
      averagePrice: "10",
    },
  });
  return response.json() as { id: string };
}

function addTransaction(positionId: string, body: object) {
  return app.inject({
    method: "POST",
    url: `/api/positions/${positionId}/transactions`,
    payload: body,
  });
}

function listTransactions(positionId: string) {
  return app.inject({
    method: "GET",
    url: `/api/positions/${positionId}/transactions`,
  });
}

function deleteTransaction(id: string) {
  return app.inject({ method: "DELETE", url: `/api/transactions/${id}` });
}

async function portfolioPosition() {
  const response = await app.inject({ method: "GET", url: "/api/portfolio" });
  return response.json().positions[0];
}

describe("transaction routes", () => {
  it("GET lists the initial BUY of a new position", async () => {
    const { id } = await createPosition();
    const response = await listTransactions(id);
    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual([
      {
        id: expect.any(String),
        positionId: id,
        type: "BUY",
        date: today,
        quantity: "100",
        unitPrice: "10",
        fees: "0",
        currency: "BRL",
        createdAt: expect.any(String),
      },
    ]);
  });

  it("POST BUY returns 201 with the new average price", async () => {
    const { id } = await createPosition();
    const response = await addTransaction(id, {
      type: "BUY",
      date: today,
      quantity: "300",
      unitPrice: "14",
      fees: "8",
    });
    expect(response.statusCode).toBe(201);
    // (100 × 10 + 300 × 14 + 8) ÷ 400
    expect(response.json()).toMatchObject({
      id,
      quantity: "400",
      averagePrice: "13.02",
    });
  });

  it("POST SELL keeps the average price and realizes profit", async () => {
    const { id } = await createPosition();
    const response = await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "40",
      unitPrice: "15",
      fees: "4.90",
    });
    expect(response.statusCode).toBe(201);
    expect(response.json()).toMatchObject({
      quantity: "60",
      averagePrice: "10",
    });
    expect(await portfolioPosition()).toMatchObject({
      quantity: "60",
      realizedProfit: "195.1",
    });
  });

  it("mirrors the derived values into the position row", async () => {
    const { id } = await createPosition();
    await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "40",
      unitPrice: "15",
    });
    const row = await prisma.position.findUniqueOrThrow({ where: { id } });
    expect(row.quantity.toString()).toBe("60");
    expect(row.averagePrice.toString()).toBe("10");
  });

  it("orders transactions by date, then by when they were recorded", async () => {
    const { id } = await createPosition();
    await addTransaction(id, {
      type: "BUY",
      date: "2026-01-10",
      quantity: "1",
      unitPrice: "1",
    });
    await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "1",
      unitPrice: "1",
    });
    const types = (await listTransactions(id))
      .json()
      .map((t: { date: string; type: string }) => `${t.date}:${t.type}`);
    expect(types).toEqual(["2026-01-10:BUY", `${today}:BUY`, `${today}:SELL`]);
  });

  it("POST returns 409 for a sell larger than the quantity held", async () => {
    const { id } = await createPosition();
    const response = await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "101",
      unitPrice: "10",
    });
    expect(response.statusCode).toBe(409);
    expect(response.json()).toEqual({
      error: {
        code: "INSUFFICIENT_QUANTITY",
        message: `Can't sell 101 on ${today}: only 100 held at that point`,
      },
    });
    // Nothing was saved.
    expect((await listTransactions(id)).json()).toHaveLength(1);
  });

  it("POST returns 409 for a sell dated before the shares were bought", async () => {
    const { id } = await createPosition();
    const response = await addTransaction(id, {
      type: "SELL",
      date: "2026-01-05",
      quantity: "10",
      unitPrice: "10",
    });
    expect(response.statusCode).toBe(409);
    expect(response.json().error.code).toBe("INSUFFICIENT_QUANTITY");
  });

  it("allows selling everything, then keeps the closed position", async () => {
    const { id } = await createPosition();
    await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "100",
      unitPrice: "12",
    });
    expect(await portfolioPosition()).toMatchObject({
      quantity: "0",
      currentValue: null,
      realizedProfit: "200",
    });
  });

  it.each([
    [{ quantity: "10.5" }, "quantity: B3 quantity must be a whole number"],
    [{ fees: "-1" }, "fees: Fees must be zero or a positive number"],
    [{ type: "SHORT" }, expect.stringContaining("type:")],
    [{ date: "2999-01-01" }, "date: Date can't be in the future"],
  ])("POST returns 400 for invalid body %j", async (change, message) => {
    const { id } = await createPosition();
    const response = await addTransaction(id, {
      type: "BUY",
      date: today,
      quantity: "10",
      unitPrice: "10",
      ...change,
    });
    expect(response.statusCode).toBe(400);
    expect(response.json()).toEqual({
      error: { code: "VALIDATION_ERROR", message },
    });
  });

  it("accepts fractional quantities on US positions", async () => {
    const { id } = await createPosition("US");
    const response = await addTransaction(id, {
      type: "BUY",
      date: today,
      quantity: "0.5",
      unitPrice: "10",
    });
    expect(response.statusCode).toBe(201);
    expect(response.json().quantity).toBe("100.5");
  });

  it("returns 404 for an unknown position", async () => {
    const list = await listTransactions("missing");
    expect(list.statusCode).toBe(404);
    expect(list.json().error.code).toBe("POSITION_NOT_FOUND");

    const create = await addTransaction("missing", {
      type: "BUY",
      date: today,
      quantity: "1",
      unitPrice: "1",
    });
    expect(create.statusCode).toBe(404);
  });

  it("DELETE removes a transaction (204) and recalculates", async () => {
    const { id } = await createPosition();
    await addTransaction(id, {
      type: "BUY",
      date: today,
      quantity: "100",
      unitPrice: "20",
    });
    const added = (await listTransactions(id)).json()[1];

    const response = await deleteTransaction(added.id);
    expect(response.statusCode).toBe(204);
    expect(await portfolioPosition()).toMatchObject({
      quantity: "100",
      averagePrice: "10",
    });
  });

  it("DELETE returns 409 when a later sell depends on the transaction", async () => {
    const { id } = await createPosition();
    await addTransaction(id, {
      type: "SELL",
      date: today,
      quantity: "50",
      unitPrice: "12",
    });
    const initialBuy = (await listTransactions(id)).json()[0];

    const response = await deleteTransaction(initialBuy.id);
    expect(response.statusCode).toBe(409);
    expect(response.json().error).toEqual({
      code: "INSUFFICIENT_QUANTITY",
      message: `Can't delete this transaction. Can't sell 50 on ${today}: only 0 held at that point`,
    });
    expect((await listTransactions(id)).json()).toHaveLength(2);
  });

  it("DELETE of the last transaction leaves an empty position", async () => {
    const { id } = await createPosition();
    const [initialBuy] = (await listTransactions(id)).json();
    expect((await deleteTransaction(initialBuy.id)).statusCode).toBe(204);
    expect(await portfolioPosition()).toMatchObject({
      quantity: "0",
      averagePrice: "0",
      realizedProfit: "0",
    });
  });

  it("DELETE returns 404 for an unknown transaction", async () => {
    const response = await deleteTransaction("missing");
    expect(response.statusCode).toBe(404);
    expect(response.json().error.code).toBe("TRANSACTION_NOT_FOUND");
  });
});
