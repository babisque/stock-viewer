import { describe, expect, it } from "vitest";
import {
  formatTime,
  formatTradeDate,
  signClass,
  sourceAndTime,
} from "./display";

describe("sourceAndTime", () => {
  it("shows the source label and the local HH:MM time", () => {
    const fetchedAt = "2026-10-12T14:32:00.000Z";
    const local = new Date(fetchedAt);
    const hhmm = `${String(local.getHours()).padStart(2, "0")}:${String(local.getMinutes()).padStart(2, "0")}`;
    expect(sourceAndTime("yahoo", fetchedAt)).toBe(`Yahoo · ${hhmm}`);
    expect(sourceAndTime("brapi", fetchedAt)).toBe(`brapi · ${hhmm}`);
  });
});

describe("formatTime", () => {
  it("formats a date, ISO string or timestamp as local HH:MM", () => {
    const local = new Date(2026, 9, 12, 9, 5);
    expect(formatTime(local)).toBe("09:05");
    expect(formatTime(local.getTime())).toBe("09:05");
    expect(formatTime(local.toISOString())).toBe("09:05");
  });
});

describe("signClass", () => {
  it("is green for gains, red for losses and neutral for zero", () => {
    expect(signClass("10")).toBe("text-gain");
    expect(signClass("-0.5")).toBe("text-loss");
    expect(signClass("0")).toBe("");
    // rounds like the displayed value, so "$0.00" is never coloured
    expect(signClass("-0.001")).toBe("");
  });
});

describe("formatTradeDate", () => {
  it("formats an ISO date as dd/mm/yyyy without shifting the day", () => {
    expect(formatTradeDate("2026-10-12")).toBe("12/10/2026");
    expect(formatTradeDate("2026-01-01")).toBe("01/01/2026");
  });
});
