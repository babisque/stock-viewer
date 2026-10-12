import type { FxRate } from "@stock-viewer/shared";
import { z } from "zod";
import { createTtlCache } from "../prices/cache";
import type { Logger } from "../prices/types";

export const AWESOMEAPI_URL =
  "https://economia.awesomeapi.com.br/json/last/USD-BRL";
export const FX_TTL_MS = 15 * 60 * 1000;
const TIMEOUT_MS = 5_000;
const CACHE_KEY = "USD-BRL";

const AwesomeApiSchema = z.object({
  USDBRL: z.object({
    bid: z
      .string()
      .regex(/^\d+(\.\d+)?$/)
      .refine((bid) => Number(bid) > 0),
  }),
});

// `stale` is true when the source failed and the last known rate is returned.
// `fx` is null only when no rate was ever fetched.
export type RateResult = { fx: FxRate | null; stale: boolean };

async function fetchRate(now: () => number): Promise<FxRate> {
  const response = await fetch(AWESOMEAPI_URL, {
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const body = AwesomeApiSchema.parse(await response.json());
  return {
    rate: body.USDBRL.bid,
    source: "awesomeapi",
    fetchedAt: new Date(now()).toISOString(),
  };
}

export function createFxService({
  log,
  now = Date.now,
}: {
  log: Logger;
  now?: () => number;
}) {
  const cache = createTtlCache<FxRate>({ ttlMs: FX_TTL_MS, now });

  async function getRate(): Promise<RateResult> {
    const cached = cache.get(CACHE_KEY);
    if (cached) return { fx: cached, stale: false };

    try {
      const fx = await fetchRate(now);
      cache.set(CACHE_KEY, fx);
      return { fx, stale: false };
    } catch (error) {
      log.warn({ provider: "awesomeapi", err: error }, "FX provider failed");
      const stale = cache.getStale(CACHE_KEY);
      return stale ? { fx: stale, stale: true } : { fx: null, stale: false };
    }
  }

  return {
    getRate,
    // Next getRate() call goes to the source again (the stale value is kept).
    clear: () => cache.expireAll(),
  };
}

export type FxService = ReturnType<typeof createFxService>;
