import { describe, expect, it } from "vitest";
import {
  AUTO_REFRESH_MS,
  cooldownRemaining,
  MIN_REFRESH_MS,
} from "./usePortfolio";

describe("refresh intervals", () => {
  it("never refresh more often than every 60 seconds", () => {
    expect(MIN_REFRESH_MS).toBeGreaterThanOrEqual(60_000);
    expect(AUTO_REFRESH_MS).toBeGreaterThanOrEqual(MIN_REFRESH_MS);
    expect(AUTO_REFRESH_MS).toBe(5 * 60_000);
  });
});

describe("cooldownRemaining", () => {
  const updatedAt = 1_000_000;

  it("counts down whole seconds from the last update", () => {
    expect(cooldownRemaining(updatedAt, updatedAt)).toBe(60);
    expect(cooldownRemaining(updatedAt + 1, updatedAt)).toBe(60);
    expect(cooldownRemaining(updatedAt + 59_001, updatedAt)).toBe(1);
  });

  it("is 0 once 60 seconds have passed", () => {
    expect(cooldownRemaining(updatedAt + 60_000, updatedAt)).toBe(0);
    expect(cooldownRemaining(updatedAt + 600_000, updatedAt)).toBe(0);
  });
});
