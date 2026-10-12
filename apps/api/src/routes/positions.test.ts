import { describe, expect, it } from "vitest";
import { setupTestContext } from "../../test/helpers";

const { app } = setupTestContext();

const petr4 = {
  ticker: " petr4 ",
  market: "B3",
  quantity: "100",
  averagePrice: "28.45",
};

async function create(body: object) {
  return app.inject({ method: "POST", url: "/api/positions", payload: body });
}

describe("positions routes", () => {
  it("POST creates a position (201) with a normalized ticker", async () => {
    const response = await create(petr4);
    expect(response.statusCode).toBe(201);
    expect(response.json()).toMatchObject({
      ticker: "PETR4",
      market: "B3",
      currency: "BRL",
      quantity: "100",
      averagePrice: "28.45",
      notes: null,
    });
  });

  it("GET lists positions", async () => {
    await create(petr4);
    const response = await app.inject({ method: "GET", url: "/api/positions" });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toHaveLength(1);
  });

  it("keeps decimal precision and returns decimals as strings", async () => {
    const response = await create({
      ticker: "AAPL",
      market: "US",
      quantity: "0.12345678",
      averagePrice: "190.12345678",
    });
    expect(response.json()).toMatchObject({
      quantity: "0.12345678",
      averagePrice: "190.12345678",
    });
  });

  it.each([
    [
      { ...petr4, quantity: "-1" },
      "quantity: Quantity must be a positive number",
    ],
    [{ ...petr4, ticker: "" }, "ticker: Ticker is required"],
    [{ ...petr4, quantity: 100 }, "quantity: Quantity is required"],
    [
      { ...petr4, quantity: "10.5" },
      "quantity: B3 quantity must be a whole number",
    ],
  ])("POST returns 400 for invalid body %j", async (body, message) => {
    const response = await create(body);
    expect(response.statusCode).toBe(400);
    expect(response.json()).toEqual({
      error: { code: "VALIDATION_ERROR", message },
    });
  });

  it("POST returns 400 for malformed JSON", async () => {
    const response = await app.inject({
      method: "POST",
      url: "/api/positions",
      headers: { "content-type": "application/json" },
      payload: "{not json",
    });
    expect(response.statusCode).toBe(400);
    expect(response.json().error.code).toBe("VALIDATION_ERROR");
  });

  it("POST returns 409 for a duplicate ticker+market", async () => {
    await create(petr4);
    const response = await create({ ...petr4, ticker: "PETR4" });
    expect(response.statusCode).toBe(409);
    expect(response.json()).toEqual({
      error: {
        code: "DUPLICATE_POSITION",
        message: "A position for PETR4 (B3) already exists",
      },
    });
  });

  it("PUT updates the notes and ignores ticker/market", async () => {
    const { id } = (await create(petr4)).json();
    const response = await app.inject({
      method: "PUT",
      url: `/api/positions/${id}`,
      payload: { ticker: "VALE3", notes: "long term" },
    });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toMatchObject({
      ticker: "PETR4",
      quantity: "100",
      averagePrice: "28.45",
      notes: "long term",
    });
  });

  it("PUT returns 409 when changing a derived value", async () => {
    const { id } = (await create(petr4)).json();
    const response = await app.inject({
      method: "PUT",
      url: `/api/positions/${id}`,
      payload: { quantity: "200", averagePrice: "28.45" },
    });
    expect(response.statusCode).toBe(409);
    expect(response.json()).toEqual({
      error: {
        code: "DERIVED_VALUE_READ_ONLY",
        message:
          "Quantity is calculated from the transactions; add a buy or sell instead",
      },
    });
  });

  it("PUT and DELETE return 404 for unknown ids", async () => {
    const put = await app.inject({
      method: "PUT",
      url: "/api/positions/missing",
      payload: { notes: null },
    });
    expect(put.statusCode).toBe(404);
    expect(put.json().error.code).toBe("POSITION_NOT_FOUND");

    const del = await app.inject({
      method: "DELETE",
      url: "/api/positions/missing",
    });
    expect(del.statusCode).toBe(404);
  });

  it("DELETE removes a position (204)", async () => {
    const { id } = (await create(petr4)).json();
    const response = await app.inject({
      method: "DELETE",
      url: `/api/positions/${id}`,
    });
    expect(response.statusCode).toBe(204);
    const list = await app.inject({ method: "GET", url: "/api/positions" });
    expect(list.json()).toEqual([]);
  });
});
