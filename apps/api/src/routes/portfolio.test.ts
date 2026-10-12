import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it } from "vitest";
import { fakeLogger, fakeProvider, FIXED_TIME } from "../../test/fakes";
import { setupTestContext } from "../../test/helpers";
import { server } from "../../test/msw";
import { AWESOMEAPI_URL, createFxService, type FxService } from "../fx/usd-brl";
import { createQuoteService } from "../prices/get-quotes";

const yahoo = fakeProvider("yahoo", { PETR4: "30", AAPL: "90" });
const quotes = createQuoteService({
  providers: [yahoo.provider],
  log: fakeLogger(),
});
// A fresh FX service per test (see beforeEach), so no stale rate leaks
// between tests; the app talks to whichever one is current.
let currentFx = createFxService({ log: fakeLogger() });
const fx: FxService = {
  getRate: () => currentFx.getRate(),
  clear: () => currentFx.clear(),
};
const { app, prisma } = setupTestContext({ quotes, fx });

function fxReturns(bid: string | null) {
  server.use(
    http.get(AWESOMEAPI_URL, () =>
      bid === null
        ? HttpResponse.json({}, { status: 500 })
        : HttpResponse.json({ USDBRL: { bid } }),
    ),
  );
}

// Quantity and average price come from the transactions, so every position
// gets its initial BUY, as the API would record it.
async function addPosition(position: {
  ticker: string;
  market: "B3" | "US";
  quantity: string;
  averagePrice: string;
}) {
  return prisma.position.create({
    data: {
      ...position,
      transactions: {
        create: {
          type: "BUY",
          date: new Date(FIXED_TIME),
          quantity: position.quantity,
          unitPrice: position.averagePrice,
        },
      },
    },
  });
}

async function addPositions() {
  await addPosition({
    ticker: "PETR4",
    market: "B3",
    quantity: "100",
    averagePrice: "20",
  });
  await addPosition({
    ticker: "AAPL",
    market: "US",
    quantity: "2",
    averagePrice: "100",
  });
}

beforeEach(() => {
  // Start every test with empty caches and a fresh call count.
  quotes.clear();
  currentFx = createFxService({ log: fakeLogger() });
  yahoo.getQuotes.mockClear();
});

describe("GET /api/portfolio", () => {
  it("returns positions with quotes, profit, totals and the FX rate", async () => {
    await addPositions();
    fxReturns("5");

    const response = await app.inject({ method: "GET", url: "/api/portfolio" });

    expect(response.statusCode).toBe(200);
    const body = response.json();
    expect(body.positions).toMatchObject([
      {
        ticker: "PETR4",
        currency: "BRL",
        quote: { price: "30", source: "yahoo", fetchedAt: FIXED_TIME },
        currentValue: "3000",
        profit: "1000",
        profitPercent: "50",
      },
      {
        ticker: "AAPL",
        currency: "USD",
        currentValue: "180",
        profit: "-20",
        profitPercent: "-10",
      },
    ]);
    expect(body.totals.BRL).toEqual({
      invested: "3000",
      currentValue: "3900",
      profit: "900",
      profitPercent: "30",
      realizedProfit: "0",
    });
    expect(body.fx).toMatchObject({ rate: "5", source: "awesomeapi" });
    expect(body.warnings).toEqual([]);
  });

  it("shows an unpriced position with a warning instead of failing", async () => {
    await addPosition({
      ticker: "XXXX3",
      market: "B3",
      quantity: "1",
      averagePrice: "1",
    });
    fxReturns("5");

    const body = (
      await app.inject({ method: "GET", url: "/api/portfolio" })
    ).json();

    expect(body.positions[0]).toMatchObject({ ticker: "XXXX3", quote: null });
    expect(body.warnings).toEqual([
      "XXXX3 (B3): price unavailable — excluded from totals",
    ]);
  });

  it("returns null totals when the FX rate is unavailable", async () => {
    await addPositions();
    fxReturns(null);

    const body = (
      await app.inject({ method: "GET", url: "/api/portfolio" })
    ).json();

    expect(body.fx).toBeNull();
    expect(body.totals).toBeNull();
    expect(body.positions).toHaveLength(2);
  });

  it("doesn't fetch quotes for closed positions", async () => {
    const { id } = await addPosition({
      ticker: "PETR4",
      market: "B3",
      quantity: "100",
      averagePrice: "20",
    });
    await prisma.transaction.create({
      data: {
        positionId: id,
        type: "SELL",
        date: new Date(FIXED_TIME),
        quantity: "100",
        unitPrice: "25",
      },
    });
    fxReturns("5");

    const body = (
      await app.inject({ method: "GET", url: "/api/portfolio" })
    ).json();

    expect(yahoo.getQuotes).not.toHaveBeenCalled();
    expect(body.positions[0]).toMatchObject({
      quantity: "0",
      quote: null,
      realizedProfit: "500",
    });
    expect(body.warnings).toEqual([]);
    expect(body.totals.BRL.realizedProfit).toBe("500");
  });

  it("does not call the providers again within the cache TTL", async () => {
    await addPositions();
    fxReturns("5");
    await app.inject({ method: "GET", url: "/api/portfolio" });
    await app.inject({ method: "GET", url: "/api/portfolio" });
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(1);
  });
});

describe("POST /api/portfolio/refresh", () => {
  it("bypasses the cache and returns a fresh payload", async () => {
    await addPositions();
    fxReturns("5");
    await app.inject({ method: "GET", url: "/api/portfolio" });

    fxReturns("6");
    const response = await app.inject({
      method: "POST",
      url: "/api/portfolio/refresh",
    });

    expect(response.statusCode).toBe(200);
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(2);
    expect(response.json().fx.rate).toBe("6");
  });
});

describe("GET /api/fx/usd-brl", () => {
  it("returns the rate", async () => {
    fxReturns("5.4321");
    const response = await app.inject({
      method: "GET",
      url: "/api/fx/usd-brl",
    });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toMatchObject({
      rate: "5.4321",
      source: "awesomeapi",
    });
  });

  it("returns 503 when no rate is available", async () => {
    fxReturns(null);
    const response = await app.inject({
      method: "GET",
      url: "/api/fx/usd-brl",
    });
    expect(response.statusCode).toBe(503);
    expect(response.json()).toEqual({
      error: { code: "FX_UNAVAILABLE", message: "USD/BRL rate is unavailable" },
    });
  });
});
