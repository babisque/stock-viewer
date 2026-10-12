import { describe, expect, it } from "vitest";
import {
  makeClosedPosition,
  makePortfolio,
  makePortfolioPosition,
  makePosition,
  makeUnpricedPosition,
  makeUsPortfolioPosition,
} from "@/test/fixtures";
import { marketAllocation, positionAllocation } from "./allocation-data";

// PETR4 R$ 3.000 + AAPL $180 (R$ 900 at 5) + an unpriced position.
const portfolio = makePortfolio({
  positions: [
    makePortfolioPosition(),
    makeUsPortfolioPosition(),
    makeUnpricedPosition(),
  ],
});

function summary(data: {
  slices: { label: string; value: unknown; percent: unknown; fill: string }[];
}) {
  return data.slices.map((slice) => [
    slice.label,
    String(slice.value),
    String(slice.percent),
    slice.fill,
  ]);
}

describe("marketAllocation", () => {
  it("splits current value by market in the selected currency", () => {
    const data = marketAllocation(portfolio, "BRL")!;
    expect(summary(data)).toEqual([
      ["B3", "3000", "76.923076923076923077", "var(--chart-1)"],
      ["US", "900", "23.076923076923076923", "var(--chart-2)"],
    ]);
    expect(data.note).toBe("1 position without a price not included.");
    expect(summary(marketAllocation(portfolio, "USD")!)[0]![1]).toBe("600");
  });

  it("is unavailable without a rate", () => {
    expect(marketAllocation({ ...portfolio, fx: null }, "BRL")).toBeNull();
  });

  it("leaves closed positions out without mentioning them", () => {
    const withClosed = {
      ...portfolio,
      positions: [...portfolio.positions, makeClosedPosition()],
    };
    const data = marketAllocation(withClosed, "BRL")!;
    expect(summary(data).map(([label, value]) => [label, value])).toEqual([
      ["B3", "3000"],
      ["US", "900"],
    ]);
    expect(data.note).toBe("1 position without a price not included.");
  });
});

describe("positionAllocation", () => {
  it("shows each priced position, largest first", () => {
    const data = positionAllocation(portfolio, "BRL");
    expect(data.slices.map((slice) => slice.label)).toEqual(["PETR4", "AAPL"]);
    expect(data.slices[0]!.angle).toBeCloseTo(76.92, 2);
  });

  it("groups everything past the 7 largest into Other", () => {
    const positions = Array.from({ length: 10 }, (_, index) =>
      makePortfolioPosition({
        ...makePosition({ id: `p${index}`, ticker: `T${index}` }),
        currentValue: String(100 - index), // T0 is the largest
      }),
    );
    const data = positionAllocation(makePortfolio({ positions }), "BRL");
    expect(data.slices).toHaveLength(8);
    expect(data.slices.at(-1)).toMatchObject({
      label: "Other",
      fill: "var(--muted-foreground)",
    });
    expect(String(data.slices.at(-1)!.value)).toBe("276"); // 93 + 92 + 91
  });

  it("colours follow the portfolio order, not the size rank", () => {
    const small = makePortfolioPosition({ currentValue: "10" });
    const big = makeUsPortfolioPosition();
    const data = positionAllocation(
      makePortfolio({ positions: [small, big] }),
      "BRL",
    );
    expect(summary(data).map(([label, , , fill]) => [label, fill])).toEqual([
      ["AAPL", "var(--chart-2)"],
      ["PETR4", "var(--chart-1)"],
    ]);
  });

  it("labels a ticker that exists on both markets with its market", () => {
    const us = makeUsPortfolioPosition({ ticker: "PETR4" });
    const data = positionAllocation(
      makePortfolio({ positions: [makePortfolioPosition(), us] }),
      "BRL",
    );
    expect(data.slices.map((slice) => slice.label)).toEqual([
      "PETR4 (B3)",
      "PETR4 (US)",
    ]);
  });

  it("shows only positions in the selected currency without a rate", () => {
    const data = positionAllocation({ ...portfolio, fx: null }, "BRL");
    expect(summary(data)).toEqual([["PETR4", "3000", "100", "var(--chart-1)"]]);
    expect(data.note).toBe(
      "1 position without a price not included. Only BRL positions are shown: the USD/BRL rate is unavailable.",
    );
  });
});
