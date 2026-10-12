import { describe, expect, it, vi } from "vitest";
import { FIXED_TIME } from "../../test/fakes";
import { createYahooProvider, toYahooSymbol } from "./yahoo";

const now = () => new Date(FIXED_TIME);

describe("toYahooSymbol", () => {
  it("adds .SA for B3 tickers only", () => {
    expect(toYahooSymbol("PETR4", "B3")).toBe("PETR4.SA");
    expect(toYahooSymbol("AAPL", "US")).toBe("AAPL");
    expect(toYahooSymbol("BRK.B", "US")).toBe("BRK.B");
  });
});

describe("createYahooProvider", () => {
  it("fetches every ticker in one batched call and maps results back", async () => {
    const quote = vi.fn(async () => [
      { symbol: "PETR4.SA", regularMarketPrice: 38.45, currency: "BRL" },
      { symbol: "AAPL", regularMarketPrice: 230.1, currency: "USD" },
    ]);
    const provider = createYahooProvider({ quote, now });

    const quotes = await provider.getQuotes([
      { ticker: "PETR4", market: "B3" },
      { ticker: "AAPL", market: "US" },
    ]);

    expect(quote).toHaveBeenCalledTimes(1);
    expect(quote).toHaveBeenCalledWith(["PETR4.SA", "AAPL"]);
    expect(quotes).toEqual([
      {
        ticker: "PETR4",
        market: "B3",
        price: "38.45",
        currency: "BRL",
        source: "yahoo",
        fetchedAt: FIXED_TIME,
      },
      {
        ticker: "AAPL",
        market: "US",
        price: "230.1",
        currency: "USD",
        source: "yahoo",
        fetchedAt: FIXED_TIME,
      },
    ]);
  });

  it("leaves out symbols without a price or with an unexpected currency", async () => {
    const provider = createYahooProvider({
      now,
      quote: async () => [
        { symbol: "PETR4.SA" },
        { symbol: "VALE3.SA", regularMarketPrice: 60, currency: "USD" },
      ],
    });
    const quotes = await provider.getQuotes([
      { ticker: "PETR4", market: "B3" },
      { ticker: "VALE3", market: "B3" },
      { ticker: "XXXX3", market: "B3" },
    ]);
    expect(quotes).toEqual([]);
  });

  it("throws a ProviderError when the call fails", async () => {
    const provider = createYahooProvider({
      now,
      quote: async () => {
        throw new Error("network down");
      },
    });
    await expect(
      provider.getQuotes([{ ticker: "AAPL", market: "US" }]),
    ).rejects.toMatchObject({ provider: "yahoo", isProviderError: true });
  });

  it("throws a ProviderError for an unexpected response shape", async () => {
    const provider = createYahooProvider({
      now,
      quote: async () => [{ symbol: "AAPL", regularMarketPrice: "230" }],
    });
    await expect(
      provider.getQuotes([{ ticker: "AAPL", market: "US" }]),
    ).rejects.toMatchObject({ message: "yahoo: unexpected response" });
  });
});
