import { describe, expect, it } from "vitest";
import {
  formatMoney,
  formatPercent,
  formatQuantity,
  formatShare,
  formatSignedMoney,
  normalizeDecimalInput,
} from "./format";

// Intl uses non-breaking spaces in pt-BR currency output.
const normalizeSpaces = (value: string) => value.replace(/\s/g, " ");

describe("formatMoney", () => {
  it("formats BRL in pt-BR", () => {
    expect(normalizeSpaces(formatMoney("2845", "BRL"))).toBe("R$ 2.845,00");
  });

  it("formats USD in en-US", () => {
    expect(formatMoney("285.3", "USD")).toBe("$285.30");
  });

  it("rounds half up only for display", () => {
    expect(formatMoney("0.125", "USD")).toBe("$0.13");
    expect(formatMoney("23.481739556", "USD")).toBe("$23.48");
  });

  it("handles large values without precision loss", () => {
    expect(formatMoney("12345678901234.565", "USD")).toBe(
      "$12,345,678,901,234.57",
    );
  });
});

describe("formatQuantity", () => {
  it("formats per market locale without trailing zeros", () => {
    expect(formatQuantity("1000", "B3")).toBe("1.000");
    expect(formatQuantity("1.50000000", "US")).toBe("1.5");
    expect(formatQuantity("0.12345678", "US")).toBe("0.12345678");
  });
});

describe("normalizeDecimalInput", () => {
  it("converts a decimal comma to a dot", () => {
    expect(normalizeDecimalInput(" 28,45 ")).toBe("28.45");
    expect(normalizeDecimalInput("28.45")).toBe("28.45");
  });
});

describe("formatSignedMoney", () => {
  it("prefixes gains with + and losses with a minus sign", () => {
    expect(normalizeSpaces(formatSignedMoney("10", "BRL"))).toBe("+R$ 10,00");
    expect(formatSignedMoney("-5", "USD")).toBe("\u2212$5.00");
  });

  it("shows no sign for zero, including values that round to zero", () => {
    expect(formatSignedMoney("0", "USD")).toBe("$0.00");
    expect(formatSignedMoney("-0.001", "USD")).toBe("$0.00");
  });
});

describe("formatPercent", () => {
  it("formats a signed percentage with 2 decimals", () => {
    expect(formatPercent("12.345")).toBe("+12.35%");
    expect(formatPercent("-5")).toBe("\u22125.00%");
    expect(formatPercent("0")).toBe("0.00%");
  });
});

describe("formatShare", () => {
  it("formats an unsigned percentage with 2 decimals", () => {
    expect(formatShare("83.333333")).toBe("83.33%");
    expect(formatShare("16.666666")).toBe("16.67%");
    expect(formatShare("100")).toBe("100.00%");
  });
});
