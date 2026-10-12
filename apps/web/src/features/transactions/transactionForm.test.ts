import { describe, expect, it } from "vitest";
import {
  emptyTransactionValues,
  normalizeTransactionValues,
  transactionFormSchema,
  transactionTotal,
} from "./transactionForm";

const values = {
  ...emptyTransactionValues("2026-10-01"),
  quantity: "10",
  unitPrice: "28,45",
  fees: "4,90",
};

describe("normalizeTransactionValues", () => {
  it("accepts comma decimals and treats empty fees as 0", () => {
    expect(normalizeTransactionValues({ ...values, fees: " " })).toEqual({
      type: "BUY",
      date: "2026-10-01",
      quantity: "10",
      unitPrice: "28.45",
      fees: "0",
    });
  });
});

describe("transactionFormSchema", () => {
  const b3 = transactionFormSchema("B3", "100");

  it("outputs the API body with dot decimals", () => {
    expect(b3.parse(values)).toEqual({
      type: "BUY",
      date: "2026-10-01",
      quantity: "10",
      unitPrice: "28.45",
      fees: "4.90",
    });
  });

  it("rejects fractional B3 quantities but accepts them on US", () => {
    const fractional = { ...values, quantity: "1,5" };
    expect(b3.safeParse(fractional).error?.issues[0]).toMatchObject({
      path: ["quantity"],
      message: "B3 quantity must be a whole number",
    });
    expect(transactionFormSchema("US", "100").parse(fractional).quantity).toBe(
      "1.5",
    );
  });

  it("rejects selling more than is held", () => {
    const sell = { ...values, type: "SELL" as const };
    expect(b3.parse({ ...sell, quantity: "100" }).quantity).toBe("100");
    expect(
      b3.safeParse({ ...sell, quantity: "101" }).error?.issues[0],
    ).toMatchObject({
      path: ["quantity"],
      message: "You can sell at most 100",
    });
  });

  it("allows buying more than is held", () => {
    expect(b3.parse({ ...values, quantity: "500" }).quantity).toBe("500");
  });
});

describe("transactionTotal", () => {
  it("adds fees to a buy and subtracts them from a sell", () => {
    expect(transactionTotal(values)?.toString()).toBe("289.4");
    expect(transactionTotal({ ...values, type: "SELL" })?.toString()).toBe(
      "279.6",
    );
  });

  it("treats empty inputs as 0 and invalid ones as unknown", () => {
    expect(transactionTotal({ ...values, fees: "" })?.toString()).toBe("284.5");
    expect(transactionTotal({ ...values, quantity: "abc" })).toBeNull();
  });
});
