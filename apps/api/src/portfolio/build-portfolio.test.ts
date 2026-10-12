import type { FxRate, Quote } from "@stock-viewer/shared";
import { describe, expect, it } from "vitest";
import { FIXED_TIME } from "../../test/fakes";
import type { LedgerPosition } from "../positions/positions";
import { buildPortfolio } from "./build-portfolio";

function position(overrides: Partial<LedgerPosition>): LedgerPosition {
  return {
    id: "pos-1",
    ticker: "PETR4",
    market: "B3",
    currency: "BRL",
    quantity: "100",
    averagePrice: "20",
    realizedProfit: "0",
    notes: null,
    createdAt: FIXED_TIME,
    updatedAt: FIXED_TIME,
    ...overrides,
  };
}

function quote(overrides: Partial<Quote>): Quote {
  return {
    ticker: "PETR4",
    market: "B3",
    price: "30",
    currency: "BRL",
    source: "yahoo",
    fetchedAt: FIXED_TIME,
    ...overrides,
  };
}

const fx: FxRate = { rate: "5", source: "awesomeapi", fetchedAt: FIXED_TIME };

const petr4 = position({});
const aapl = position({
  id: "pos-2",
  ticker: "AAPL",
  market: "US",
  currency: "USD",
  quantity: "2",
  averagePrice: "100",
});

describe("buildPortfolio", () => {
  it("calculates profit per position and totals in BRL and USD", () => {
    const result = buildPortfolio(
      [petr4, aapl],
      [
        quote({}),
        quote({ ticker: "AAPL", market: "US", currency: "USD", price: "90" }),
      ],
      { fx, stale: false },
    );

    expect(result.positions[0]).toMatchObject({
      ticker: "PETR4",
      quote: { price: "30", source: "yahoo" },
      currentValue: "3000",
      profit: "1000",
      profitPercent: "50",
    });
    expect(result.positions[1]).toMatchObject({
      ticker: "AAPL",
      currentValue: "180",
      profit: "-20",
      profitPercent: "-10",
    });
    // BRL: invested 2000 + 200×5 = 3000; value 3000 + 180×5 = 3900
    expect(result.totals).toEqual({
      BRL: {
        invested: "3000",
        currentValue: "3900",
        profit: "900",
        profitPercent: "30",
        realizedProfit: "0",
      },
      USD: {
        invested: "600",
        currentValue: "780",
        profit: "180",
        profitPercent: "30",
        realizedProfit: "0",
      },
    });
    expect(result.fx).toEqual(fx);
    expect(result.warnings).toEqual([]);
  });

  it("marks unpriced positions and leaves them out of the totals", () => {
    const result = buildPortfolio([petr4, aapl], [quote({})], {
      fx,
      stale: false,
    });

    expect(result.positions[1]).toMatchObject({
      ticker: "AAPL",
      quote: null,
      currentValue: null,
      profit: null,
      profitPercent: null,
    });
    expect(result.totals?.BRL).toEqual({
      invested: "2000",
      currentValue: "3000",
      profit: "1000",
      profitPercent: "50",
      realizedProfit: "0",
    });
    expect(result.warnings).toEqual([
      "AAPL (US): price unavailable — excluded from totals",
    ]);
  });

  it("matches quotes by ticker and market", () => {
    const result = buildPortfolio(
      [petr4],
      [quote({ market: "US", currency: "USD" })],
      { fx, stale: false },
    );
    expect(result.positions[0]?.quote).toBeNull();
  });

  it("returns null totals when there is no FX rate", () => {
    const result = buildPortfolio([petr4], [quote({})], {
      fx: null,
      stale: false,
    });
    expect(result.totals).toBeNull();
    expect(result.fx).toBeNull();
    expect(result.positions[0]?.profit).toBe("1000");
    expect(result.warnings).toEqual([
      "USD/BRL rate unavailable — totals not shown",
    ]);
  });

  it("warns when the FX rate is stale", () => {
    const result = buildPortfolio([], [], { fx, stale: true });
    expect(result.totals).not.toBeNull();
    expect(result.warnings).toEqual([
      "USD/BRL rate is outdated — using the last known rate",
    ]);
  });

  it("returns zero totals for an empty portfolio", () => {
    const result = buildPortfolio([], [], { fx, stale: false });
    expect(result.positions).toEqual([]);
    expect(result.totals?.USD).toEqual({
      invested: "0",
      currentValue: "0",
      profit: "0",
      profitPercent: "0",
      realizedProfit: "0",
    });
  });

  it("returns realized profit per position and converts it in the totals", () => {
    const result = buildPortfolio(
      [
        { ...petr4, realizedProfit: "150" },
        { ...aapl, realizedProfit: "-10" },
      ],
      [quote({})],
      { fx, stale: false },
    );
    expect(result.positions.map((p) => p.realizedProfit)).toEqual([
      "150",
      "-10",
    ]);
    // AAPL has no quote but its realized profit still counts: 150 − 10×5
    expect(result.totals?.BRL.realizedProfit).toBe("100");
    expect(result.totals?.USD.realizedProfit).toBe("20");
    // Unrealized profit is unchanged by the sells.
    expect(result.totals?.BRL.profit).toBe("1000");
  });

  it("keeps closed positions without values or warnings", () => {
    const closed = { ...petr4, quantity: "0", realizedProfit: "300" };
    const result = buildPortfolio([closed], [quote({})], {
      fx,
      stale: false,
    });
    expect(result.positions[0]).toMatchObject({
      quantity: "0",
      quote: null,
      currentValue: null,
      profit: null,
      profitPercent: null,
      realizedProfit: "300",
    });
    expect(result.warnings).toEqual([]);
    expect(result.totals?.BRL).toMatchObject({
      invested: "0",
      currentValue: "0",
      realizedProfit: "300",
    });
  });
});
