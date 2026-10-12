import { describe, expect, it } from "vitest";
import { portfolioTotals, profit, realizedProfitTotal } from "./profit";

function asStrings(values: Record<string, { toString(): string }>) {
  return Object.fromEntries(
    Object.entries(values).map(([key, value]) => [key, value.toString()]),
  );
}

describe("profit", () => {
  it("calculates a gain", () => {
    expect(asStrings(profit("100", "28.45", "30"))).toEqual({
      invested: "2845",
      currentValue: "3000",
      amount: "155",
      percent: expect.stringMatching(/^5\.448154657293/),
    });
  });

  it("calculates a loss", () => {
    expect(asStrings(profit("10", "20", "15"))).toEqual({
      invested: "200",
      currentValue: "150",
      amount: "-50",
      percent: "-25",
    });
  });

  it("handles fractional quantities without float errors", () => {
    const result = profit("0.12345678", "190.2", "200");
    expect(result.invested.toString()).toBe("23.481479556");
    expect(result.currentValue.toString()).toBe("24.691356");
    expect(result.amount.toString()).toBe("1.209876444");
    expect(profit("3", "0.1", "0.2").amount.toString()).toBe("0.3");
  });

  it("returns 0% when nothing was invested", () => {
    expect(profit("10", "0", "5").percent.toString()).toBe("0");
  });

  it("returns zero profit when the price did not move", () => {
    const result = profit("10", "20", "20");
    expect(result.amount.isZero()).toBe(true);
    expect(result.percent.isZero()).toBe(true);
  });
});

describe("portfolioTotals", () => {
  const items = [
    { currency: "BRL" as const, invested: "1000", currentValue: "1100" },
    { currency: "USD" as const, invested: "100", currentValue: "90" },
  ];

  it("sums in BRL, converting USD with the rate", () => {
    expect(asStrings(portfolioTotals(items, "BRL", "5"))).toEqual({
      invested: "1500",
      currentValue: "1550",
      profit: "50",
      profitPercent: expect.stringMatching(/^3\.3333/),
    });
  });

  it("sums in USD, converting BRL with the rate", () => {
    expect(asStrings(portfolioTotals(items, "USD", "5"))).toEqual({
      invested: "300",
      currentValue: "310",
      profit: "10",
      profitPercent: expect.stringMatching(/^3\.3333/),
    });
  });

  it("returns zeros for an empty portfolio", () => {
    expect(asStrings(portfolioTotals([], "BRL", "5"))).toEqual({
      invested: "0",
      currentValue: "0",
      profit: "0",
      profitPercent: "0",
    });
  });
});

describe("realizedProfitTotal", () => {
  const items = [
    { currency: "BRL" as const, realizedProfit: "200" },
    { currency: "USD" as const, realizedProfit: "-10" },
  ];

  it("converts each item to the target currency before summing", () => {
    expect(realizedProfitTotal(items, "BRL", "5").toString()).toBe("150");
    expect(realizedProfitTotal(items, "USD", "5").toString()).toBe("30");
  });

  it("is 0 without items", () => {
    expect(realizedProfitTotal([], "BRL", "5").toString()).toBe("0");
  });
});
