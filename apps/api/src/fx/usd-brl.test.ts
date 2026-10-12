import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { fakeLogger } from "../../test/fakes";
import { server } from "../../test/msw";
import { AWESOMEAPI_URL, createFxService, FX_TTL_MS } from "./usd-brl";

const START = Date.parse("2026-10-12T14:32:00.000Z");

function respondWith(...responses: (() => Response)[]) {
  let calls = 0;
  server.use(
    http.get(AWESOMEAPI_URL, () => {
      const respond = responses[Math.min(calls, responses.length - 1)]!;
      calls += 1;
      return respond();
    }),
  );
  return { calls: () => calls };
}

const ok = (bid: string) => () =>
  HttpResponse.json({ USDBRL: { code: "USD", codein: "BRL", bid, ask: "9" } });
const fail = () => HttpResponse.json({ status: 500 }, { status: 500 });

function setup() {
  let time = START;
  const log = fakeLogger();
  const service = createFxService({ log, now: () => time });
  const advance = (ms: number) => {
    time += ms;
  };
  return { service, log, advance };
}

describe("createFxService", () => {
  it("returns the bid as a decimal string", async () => {
    respondWith(ok("5.4321"));
    const { service } = setup();
    expect(await service.getRate()).toEqual({
      fx: {
        rate: "5.4321",
        source: "awesomeapi",
        fetchedAt: "2026-10-12T14:32:00.000Z",
      },
      stale: false,
    });
  });

  it("serves the cached rate within 15 minutes", async () => {
    const source = respondWith(ok("5.4"));
    const { service, advance } = setup();
    await service.getRate();
    advance(FX_TTL_MS - 1);
    await service.getRate();
    expect(source.calls()).toBe(1);
    advance(1);
    await service.getRate();
    expect(source.calls()).toBe(2);
  });

  it("falls back to the stale rate when the source fails", async () => {
    respondWith(ok("5.4"), fail);
    const { service, advance, log } = setup();
    await service.getRate();
    advance(FX_TTL_MS);

    const result = await service.getRate();
    expect(result).toMatchObject({ fx: { rate: "5.4" }, stale: true });
    expect(log.warn).toHaveBeenCalledWith(
      expect.objectContaining({ provider: "awesomeapi" }),
      "FX provider failed",
    );
  });

  it("returns null when no rate was ever fetched", async () => {
    respondWith(fail);
    const { service } = setup();
    expect(await service.getRate()).toEqual({ fx: null, stale: false });
  });

  it.each([
    ["missing USDBRL", { other: {} }],
    ["non-numeric bid", { USDBRL: { bid: "abc" } }],
    ["zero bid", { USDBRL: { bid: "0" } }],
  ])("rejects a malformed body (%s)", async (_, body) => {
    respondWith(() => HttpResponse.json(body));
    const { service } = setup();
    expect(await service.getRate()).toEqual({ fx: null, stale: false });
  });

  it("clear() refetches but keeps the stale fallback", async () => {
    const source = respondWith(ok("5.4"), fail);
    const { service } = setup();
    await service.getRate();
    service.clear();
    expect(await service.getRate()).toMatchObject({
      fx: { rate: "5.4" },
      stale: true,
    });
    expect(source.calls()).toBe(2);
  });
});
