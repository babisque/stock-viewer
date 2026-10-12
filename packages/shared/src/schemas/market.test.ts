import { describe, expect, it } from "vitest";
import { currencyForMarket } from "./market";

describe("currencyForMarket", () => {
  it("maps B3 to BRL and US to USD", () => {
    expect(currencyForMarket("B3")).toBe("BRL");
    expect(currencyForMarket("US")).toBe("USD");
  });
});
