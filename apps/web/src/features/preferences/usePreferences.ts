import { CurrencySchema, type Currency } from "@stock-viewer/shared";
import { useEffect, useState } from "react";
import { z } from "zod";

export const ThemeSchema = z.enum(["system", "light", "dark"]);
export type Theme = z.infer<typeof ThemeSchema>;

export const CURRENCY_KEY = "stock-viewer:currency";
export const THEME_KEY = "stock-viewer:theme";
const DARK_QUERY = "(prefers-color-scheme: dark)";

// localStorage can throw (private mode, blocked site data), and a stored value
// may be stale or edited by hand: either way, fall back to the default.
export function readPreference<T>(
  key: string,
  schema: z.ZodType<T>,
  fallback: T,
): T {
  try {
    const parsed = schema.safeParse(localStorage.getItem(key));
    return parsed.success ? parsed.data : fallback;
  } catch {
    return fallback;
  }
}

export function writePreference(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Not saved; the choice still applies until the page is reloaded.
  }
}

export function resolveTheme(
  theme: Theme,
  prefersDark: boolean,
): "light" | "dark" {
  if (theme === "system") return prefersDark ? "dark" : "light";
  return theme;
}

function prefersDark(): boolean {
  return window.matchMedia(DARK_QUERY).matches;
}

export function applyTheme(theme: Theme): void {
  const dark = resolveTheme(theme, prefersDark()) === "dark";
  document.documentElement.classList.toggle("dark", dark);
}

// Applies the saved theme before React renders, so the page doesn't flash.
export function applySavedTheme(): void {
  applyTheme(readPreference(THEME_KEY, ThemeSchema, "system"));
}

export function usePreferences() {
  const [currency, setCurrencyState] = useState<Currency>(() =>
    readPreference(CURRENCY_KEY, CurrencySchema, "BRL"),
  );
  const [theme, setThemeState] = useState<Theme>(() =>
    readPreference(THEME_KEY, ThemeSchema, "system"),
  );

  useEffect(() => {
    applyTheme(theme);
    if (theme !== "system") return;
    // Follow the OS setting while it changes.
    const query = window.matchMedia(DARK_QUERY);
    const onChange = () => applyTheme("system");
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, [theme]);

  const setCurrency = (value: Currency) => {
    setCurrencyState(value);
    writePreference(CURRENCY_KEY, value);
  };
  const setTheme = (value: Theme) => {
    setThemeState(value);
    writePreference(THEME_KEY, value);
  };

  return { currency, setCurrency, theme, setTheme };
}
