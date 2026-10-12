import { describe, expect, it } from "vitest";
import { createTtlCache } from "./cache";

function setup() {
  let time = 1_000;
  const cache = createTtlCache<string>({ ttlMs: 100, now: () => time });
  const advance = (ms: number) => {
    time += ms;
  };
  return { cache, advance };
}

describe("createTtlCache", () => {
  it("returns a value until its TTL passes", () => {
    const { cache, advance } = setup();
    cache.set("a", "1");
    advance(99);
    expect(cache.get("a")).toBe("1");
    advance(1);
    expect(cache.get("a")).toBeUndefined();
  });

  it("keeps expired values available through getStale", () => {
    const { cache, advance } = setup();
    cache.set("a", "1");
    advance(500);
    expect(cache.getStale("a")).toBe("1");
    expect(cache.getStale("missing")).toBeUndefined();
  });

  it("expireAll makes get miss but keeps stale values", () => {
    const { cache } = setup();
    cache.set("a", "1");
    cache.expireAll();
    expect(cache.get("a")).toBeUndefined();
    expect(cache.getStale("a")).toBe("1");
  });

  it("set refreshes the TTL", () => {
    const { cache, advance } = setup();
    cache.set("a", "1");
    advance(150);
    cache.set("a", "2");
    expect(cache.get("a")).toBe("2");
  });
});
