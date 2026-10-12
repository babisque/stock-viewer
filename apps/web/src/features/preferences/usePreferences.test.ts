import { CurrencySchema } from "@stock-viewer/shared";
import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setPrefersDark } from "@/test/setup";
import {
  CURRENCY_KEY,
  readPreference,
  resolveTheme,
  THEME_KEY,
  usePreferences,
  writePreference,
} from "./usePreferences";

afterEach(() => {
  vi.restoreAllMocks();
});

const isDark = () => document.documentElement.classList.contains("dark");

describe("readPreference / writePreference", () => {
  it("reads back a saved value", () => {
    writePreference(CURRENCY_KEY, "USD");
    expect(readPreference(CURRENCY_KEY, CurrencySchema, "BRL")).toBe("USD");
  });

  it("falls back to the default for a missing or invalid value", () => {
    expect(readPreference(CURRENCY_KEY, CurrencySchema, "BRL")).toBe("BRL");
    localStorage.setItem(CURRENCY_KEY, "EUR");
    expect(readPreference(CURRENCY_KEY, CurrencySchema, "BRL")).toBe("BRL");
  });

  it("falls back to the default when localStorage throws", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceededError");
    });
    expect(() => writePreference(CURRENCY_KEY, "USD")).not.toThrow();
    expect(readPreference(CURRENCY_KEY, CurrencySchema, "BRL")).toBe("BRL");
  });
});

describe("resolveTheme", () => {
  it.each([
    ["system", false, "light"],
    ["system", true, "dark"],
    ["light", true, "light"],
    ["dark", false, "dark"],
  ] as const)("%s with prefersDark=%s is %s", (theme, dark, expected) => {
    expect(resolveTheme(theme, dark)).toBe(expected);
  });
});

describe("usePreferences", () => {
  it("defaults to BRL and the system theme", () => {
    const { result } = renderHook(() => usePreferences());
    expect(result.current.currency).toBe("BRL");
    expect(result.current.theme).toBe("system");
    expect(isDark()).toBe(false);
  });

  it("remembers the choices across a remount", () => {
    const first = renderHook(() => usePreferences());
    act(() => {
      first.result.current.setCurrency("USD");
      first.result.current.setTheme("dark");
    });
    expect(isDark()).toBe(true);
    first.unmount();

    const second = renderHook(() => usePreferences());
    expect(second.result.current.currency).toBe("USD");
    expect(second.result.current.theme).toBe("dark");
    expect(localStorage.getItem(THEME_KEY)).toBe("dark");
  });

  it("follows the OS setting while the theme is 'system'", () => {
    renderHook(() => usePreferences());
    expect(isDark()).toBe(false);
    act(() => setPrefersDark(true));
    expect(isDark()).toBe(true);
  });
});
