import { describe, expect, it } from "vitest";
import { fakeLogger, fakeProvider } from "../../test/fakes";
import { createQuoteService, QUOTE_TTL_MS } from "./get-quotes";

const petr4 = { ticker: "PETR4", market: "B3" } as const;
const vale3 = { ticker: "VALE3", market: "B3" } as const;
const aapl = { ticker: "AAPL", market: "US" } as const;

function setup(options: { yahooFails?: boolean } = {}) {
  let time = 0;
  const yahoo = fakeProvider(
    "yahoo",
    { PETR4: "38.45", AAPL: "230.1" },
    { fail: options.yahooFails },
  );
  const brapi = fakeProvider(
    "brapi",
    { PETR4: "38.40", VALE3: "61.2" },
    { markets: ["B3"] },
  );
  const log = fakeLogger();
  const service = createQuoteService({
    providers: [yahoo.provider, brapi.provider],
    log,
    now: () => time,
  });
  const advance = (ms: number) => {
    time += ms;
  };
  return { service, yahoo, brapi, log, advance };
}

const summary = (quotes: { ticker: string; source: string; price: string }[]) =>
  quotes.map((q) => `${q.ticker}:${q.source}:${q.price}`).sort();

describe("createQuoteService", () => {
  it("uses Yahoo for B3 and US tickers in one batched call", async () => {
    const { service, yahoo, brapi } = setup();
    const quotes = await service.getQuotes([petr4, aapl]);
    expect(summary(quotes)).toEqual(["AAPL:yahoo:230.1", "PETR4:yahoo:38.45"]);
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(1);
    expect(brapi.getQuotes).not.toHaveBeenCalled();
  });

  it("falls back to brapi for B3 when Yahoo fails, and logs the failure", async () => {
    const { service, brapi, log } = setup({ yahooFails: true });
    const quotes = await service.getQuotes([petr4, aapl]);

    expect(summary(quotes)).toEqual(["PETR4:brapi:38.40"]);
    // brapi only gets the B3 tickers
    expect(brapi.getQuotes).toHaveBeenCalledWith([petr4]);
    expect(log.warn).toHaveBeenCalledWith(
      expect.objectContaining({
        provider: "yahoo",
        tickers: ["PETR4", "AAPL"],
      }),
      "Price provider failed",
    );
  });

  it("sends only the tickers Yahoo could not price to brapi", async () => {
    const { service, brapi, log } = setup();
    const quotes = await service.getQuotes([petr4, vale3]);

    expect(summary(quotes)).toEqual(["PETR4:yahoo:38.45", "VALE3:brapi:61.2"]);
    expect(brapi.getQuotes).toHaveBeenCalledWith([vale3]);
    expect(log.warn).toHaveBeenCalledWith(
      { provider: "yahoo", tickers: ["VALE3"] },
      "Price provider returned no quote",
    );
  });

  it("leaves out tickers no provider can price, without throwing", async () => {
    const { service, log } = setup({ yahooFails: true });
    const quotes = await service.getQuotes([
      aapl,
      { ticker: "XXXX3", market: "B3" },
    ]);
    expect(quotes).toEqual([]);
    expect(log.warn).toHaveBeenCalledWith(
      { provider: "brapi", tickers: ["XXXX3"] },
      "Price provider returned no quote",
    );
  });

  it("serves repeated requests from the cache within the TTL", async () => {
    const { service, yahoo, advance } = setup();
    await service.getQuotes([petr4, aapl]);
    advance(QUOTE_TTL_MS - 1);
    const quotes = await service.getQuotes([petr4, aapl]);

    expect(summary(quotes)).toEqual(["AAPL:yahoo:230.1", "PETR4:yahoo:38.45"]);
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(1);
  });

  it("only fetches the tickers missing from the cache", async () => {
    const { service, yahoo } = setup();
    await service.getQuotes([petr4]);
    await service.getQuotes([petr4, aapl]);
    expect(yahoo.getQuotes).toHaveBeenLastCalledWith([aapl]);
  });

  it("calls the providers again after the TTL", async () => {
    const { service, yahoo, advance } = setup();
    await service.getQuotes([petr4]);
    advance(QUOTE_TTL_MS);
    await service.getQuotes([petr4]);
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(2);
  });

  it("clear() forces a refetch", async () => {
    const { service, yahoo } = setup();
    await service.getQuotes([petr4]);
    service.clear();
    await service.getQuotes([petr4]);
    expect(yahoo.getQuotes).toHaveBeenCalledTimes(2);
  });

  it("deduplicates repeated tickers and skips the providers for none", async () => {
    const { service, yahoo } = setup();
    await service.getQuotes([]);
    expect(yahoo.getQuotes).not.toHaveBeenCalled();
    await service.getQuotes([petr4, { ...petr4 }]);
    expect(yahoo.getQuotes).toHaveBeenCalledWith([petr4]);
  });
});
