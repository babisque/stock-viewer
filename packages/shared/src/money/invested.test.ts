import { describe, expect, it } from "vitest";
import { investedAmount } from "./invested";

describe("investedAmount", () => {
  it("multiplies quantity by average price", () => {
    expect(investedAmount("100", "28.45").toString()).toBe("2845");
  });

  it("keeps full precision (no float errors)", () => {
    expect(investedAmount("3", "0.1").toString()).toBe("0.3");
    expect(investedAmount("0.12345678", "190.2").toString()).toBe(
      "23.481479556",
    );
  });
});
