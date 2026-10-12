import { describe, expect, it } from "vitest";
import { convert } from "./convert";

describe("convert", () => {
  it("keeps the amount when the currency is the same", () => {
    expect(convert("10.5", "BRL", "BRL", "5").toString()).toBe("10.5");
    expect(convert("10.5", "USD", "USD", "5").toString()).toBe("10.5");
  });

  it("multiplies USD by the rate to get BRL", () => {
    expect(convert("100", "USD", "BRL", "5.42").toString()).toBe("542");
  });

  it("divides BRL by the rate to get USD", () => {
    expect(convert("542", "BRL", "USD", "5.42").toString()).toBe("100");
  });

  it("converts negative amounts (losses)", () => {
    expect(convert("-10", "USD", "BRL", "5").toString()).toBe("-50");
  });

  it.each(["0", "-1"])("rejects the invalid rate %j", (rate) => {
    expect(() => convert("1", "USD", "BRL", rate)).toThrow(
      "Invalid USD/BRL rate",
    );
  });
});
