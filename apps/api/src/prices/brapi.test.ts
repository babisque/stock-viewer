import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { FIXED_TIME } from "../../test/fakes";
import { server } from "../../test/msw";
import { BRAPI_URL, createBrapiProvider } from "./brapi";

const now = () => new Date(FIXED_TIME);

describe("createBrapiProvider", () => {
  it("only supports B3", () => {
    const provider = createBrapiProvider();
    expect(provider.supports("B3")).toBe(true);
    expect(provider.supports("US")).toBe(false);
  });

  it("fetches all tickers in one request and parses the results", async () => {
    const requests: Request[] = [];
    server.use(
      http.get(`${BRAPI_URL}/:tickers`, ({ request }) => {
        requests.push(request);
        return HttpResponse.json({
          results: [
            { symbol: "PETR4", regularMarketPrice: 38.45, currency: "BRL" },
            { symbol: "VALE3", regularMarketPrice: 61.2, currency: "BRL" },
          ],
        });
      }),
    );
    const provider = createBrapiProvider({ now });

    const quotes = await provider.getQuotes([
      { ticker: "PETR4", market: "B3" },
      { ticker: "VALE3", market: "B3" },
    ]);

    expect(requests).toHaveLength(1);
    expect(new URL(requests[0]!.url).pathname).toBe("/api/quote/PETR4,VALE3");
    expect(requests[0]!.headers.get("authorization")).toBeNull();
    expect(quotes).toEqual([
      {
        ticker: "PETR4",
        market: "B3",
        price: "38.45",
        currency: "BRL",
        source: "brapi",
        fetchedAt: FIXED_TIME,
      },
      {
        ticker: "VALE3",
        market: "B3",
        price: "61.2",
        currency: "BRL",
        source: "brapi",
        fetchedAt: FIXED_TIME,
      },
    ]);
  });

  it("sends the token as a bearer header when configured", async () => {
    const onRequest = vi.fn();
    server.use(
      http.get(`${BRAPI_URL}/:tickers`, ({ request }) => {
        onRequest(request.headers.get("authorization"));
        return HttpResponse.json({ results: [] });
      }),
    );
    await createBrapiProvider({ token: "secret", now }).getQuotes([
      { ticker: "PETR4", market: "B3" },
    ]);
    expect(onRequest).toHaveBeenCalledWith("Bearer secret");
  });

  it("throws a ProviderError on HTTP errors", async () => {
    server.use(
      http.get(`${BRAPI_URL}/:tickers`, () =>
        HttpResponse.json({ error: true }, { status: 404 }),
      ),
    );
    await expect(
      createBrapiProvider({ now }).getQuotes([
        { ticker: "XXXX3", market: "B3" },
      ]),
    ).rejects.toMatchObject({ message: "brapi: HTTP 404", provider: "brapi" });
  });

  it("throws a ProviderError for an unexpected response shape", async () => {
    server.use(
      http.get(`${BRAPI_URL}/:tickers`, () =>
        HttpResponse.json({
          results: [{ symbol: "PETR4", regularMarketPrice: "38" }],
        }),
      ),
    );
    await expect(
      createBrapiProvider({ now }).getQuotes([
        { ticker: "PETR4", market: "B3" },
      ]),
    ).rejects.toMatchObject({ message: "brapi: unexpected response" });
  });

  it("throws a ProviderError on network failure", async () => {
    server.use(http.get(`${BRAPI_URL}/:tickers`, () => HttpResponse.error()));
    await expect(
      createBrapiProvider({ now }).getQuotes([
        { ticker: "PETR4", market: "B3" },
      ]),
    ).rejects.toMatchObject({ message: "brapi: request failed" });
  });
});
