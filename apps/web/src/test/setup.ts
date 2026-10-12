import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";

// jsdom has no matchMedia: fake the "prefers dark" OS setting so tests can
// flip it and the listeners registered by the theme hook fire.
let prefersDark = false;
const listeners = new Set<() => void>();

export function setPrefersDark(value: boolean) {
  prefersDark = value;
  for (const listener of listeners) listener();
}

window.matchMedia = (query: string) =>
  ({
    get matches() {
      return query.includes("dark") && prefersDark;
    },
    media: query,
    addEventListener: (_: string, listener: () => void) =>
      listeners.add(listener),
    removeEventListener: (_: string, listener: () => void) =>
      listeners.delete(listener),
  }) as unknown as MediaQueryList;

// Recharts' ResponsiveContainer needs ResizeObserver, which jsdom lacks.
globalThis.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

beforeEach(() => {
  prefersDark = false;
});

afterEach(() => {
  cleanup();
  listeners.clear();
  localStorage.clear();
  document.documentElement.classList.remove("dark");
});
