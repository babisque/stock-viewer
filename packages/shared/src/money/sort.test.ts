import { describe, expect, it } from "vitest";
import { compareDecimals, moneySortKey } from "./sort";

describe("moneySortKey", () => {
  it("keeps amounts already in the target currency", () => {
    expect(moneySortKey("100", "BRL", "BRL", "5")).toBe("100");
    expect(moneySortKey("100", "BRL", "BRL", null)).toBe("100");
  });

  it("converts amounts in the other currency", () => {
    expect(moneySortKey("100", "USD", "BRL", "5")).toBe("500");
    expect(moneySortKey("500", "BRL", "USD", "5")).toBe("100");
  });

  it("can't sort a missing amount, or another currency without a rate", () => {
    expect(moneySortKey(null, "BRL", "BRL", "5")).toBeUndefined();
    expect(moneySortKey("100", "USD", "BRL", null)).toBeUndefined();
  });
});

describe("compareDecimals", () => {
  it("compares exactly, including values a float would get wrong", () => {
    expect(compareDecimals("2", "10")).toBe(-1);
    expect(compareDecimals("-5", "-10")).toBe(1);
    expect(compareDecimals("0.3", "0.30")).toBe(0);
    expect(compareDecimals("9007199254740993.01", "9007199254740993.02")).toBe(
      -1,
    );
  });
});
