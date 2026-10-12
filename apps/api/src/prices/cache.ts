type Entry<V> = { value: V; expiresAt: number };

type TtlCacheOptions = {
  ttlMs: number;
  now?: () => number;
};

// Tiny in-memory cache. Expired entries are kept (not deleted) so callers can
// fall back to the last known value with getStale() when a source is down.
export function createTtlCache<V>({ ttlMs, now = Date.now }: TtlCacheOptions) {
  const entries = new Map<string, Entry<V>>();

  return {
    get(key: string): V | undefined {
      const entry = entries.get(key);
      return entry && entry.expiresAt > now() ? entry.value : undefined;
    },
    getStale(key: string): V | undefined {
      return entries.get(key)?.value;
    },
    set(key: string, value: V): void {
      entries.set(key, { value, expiresAt: now() + ttlMs });
    },
    // Forces the next get() to miss while keeping values for getStale().
    expireAll(): void {
      for (const entry of entries.values()) entry.expiresAt = 0;
    },
  };
}

export type TtlCache<V> = ReturnType<typeof createTtlCache<V>>;
