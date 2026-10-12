import { describe, expect, it } from "vitest";
import {
  countDecimals,
  nonNegativeDecimalString,
  positiveDecimalString,
} from "./decimal";

const schema = positiveDecimalString("Value", 2);

describe("positiveDecimalString", () => {
  it("accepts positive decimal strings", () => {
    expect(schema.parse("28.45")).toBe("28.45");
    expect(schema.parse(" 100 ")).toBe("100");
  });

  it.each([
    ["", "Value is required"],
    ["-1", "Value must be a positive number"],
    ["0", "Value must be greater than 0"],
    ["0.00", "Value must be greater than 0"],
    ["abc", "Value must be a positive number"],
    ["1e5", "Value must be a positive number"],
    ["28,45", "Value must be a positive number"],
    ["1.234", "Value can have at most 2 decimal places"],
  ])("rejects %j with %j", (input, message) => {
    const result = schema.safeParse(input);
    expect(result.success).toBe(false);
    expect(result.error?.issues[0]?.message).toBe(message);
  });
});

describe("significant digits limit", () => {
  const precise = positiveDecimalString("Value", 8);

  it("accepts up to 15 significant digits", () => {
    expect(precise.parse("1234567.12345678")).toBe("1234567.12345678");
    expect(precise.parse("0.00000001")).toBe("0.00000001");
  });

  it("rejects values that SQLite would round", () => {
    const result = precise.safeParse("9999999999.99999999");
    expect(result.error?.issues[0]?.message).toBe(
      "Value can have at most 15 significant digits",
    );
  });
});

describe("countDecimals", () => {
  it("counts digits after the dot", () => {
    expect(countDecimals("10")).toBe(0);
    expect(countDecimals("10.5")).toBe(1);
    expect(countDecimals("0.12345678")).toBe(8);
  });
});

describe("nonNegativeDecimalString", () => {
  const fees = nonNegativeDecimalString("Fees", 2);

  it("accepts zero and positive values", () => {
    expect(fees.parse("0")).toBe("0");
    expect(fees.parse(" 4.90 ")).toBe("4.90");
  });

  it.each([
    ["", "Fees is required"],
    ["-1", "Fees must be zero or a positive number"],
    ["4,90", "Fees must be zero or a positive number"],
    ["1.234", "Fees can have at most 2 decimal places"],
  ])("rejects %j with %j", (input, message) => {
    expect(fees.safeParse(input).error?.issues[0]?.message).toBe(message);
  });
});
