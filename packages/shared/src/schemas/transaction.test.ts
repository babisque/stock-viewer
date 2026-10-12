import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CreateTransactionSchema, todayIsoDate } from "./transaction";

const valid = {
  type: "BUY",
  date: "2026-10-01",
  quantity: "100",
  unitPrice: "28.45",
  fees: "4.90",
};

function firstIssue(input: unknown) {
  const result = CreateTransactionSchema.safeParse(input);
  expect(result.success).toBe(false);
  return result.error?.issues[0];
}

describe("CreateTransactionSchema", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 9, 12, 10, 0)); // local 2026-10-12
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("accepts a valid transaction", () => {
    expect(CreateTransactionSchema.parse(valid)).toEqual(valid);
  });

  it("defaults fees to 0", () => {
    const withoutFees = {
      type: valid.type,
      date: valid.date,
      quantity: valid.quantity,
      unitPrice: valid.unitPrice,
    };
    expect(CreateTransactionSchema.parse(withoutFees).fees).toBe("0");
  });

  it("accepts zero fees and today's date", () => {
    expect(
      CreateTransactionSchema.parse({
        ...valid,
        fees: "0",
        date: "2026-10-12",
      }),
    ).toMatchObject({ fees: "0", date: "2026-10-12" });
  });

  it("rejects negative fees", () => {
    expect(firstIssue({ ...valid, fees: "-1" })).toMatchObject({
      path: ["fees"],
      message: "Fees must be zero or a positive number",
    });
  });

  it("rejects an unknown type", () => {
    expect(firstIssue({ ...valid, type: "SHORT" })?.path).toEqual(["type"]);
  });

  it("rejects zero quantity and unit price", () => {
    expect(firstIssue({ ...valid, quantity: "0" })?.path).toEqual(["quantity"]);
    expect(firstIssue({ ...valid, unitPrice: "0" })?.path).toEqual([
      "unitPrice",
    ]);
  });

  it.each([
    ["", "Date is required"],
    ["12/10/2026", "Date must be in YYYY-MM-DD format"],
    ["2026-02-30", "Date is not a valid date"],
    ["2026-10-13", "Date can't be in the future"],
  ])("rejects date %j with %j", (date, message) => {
    expect(firstIssue({ ...valid, date })).toMatchObject({
      path: ["date"],
      message,
    });
  });
});

describe("todayIsoDate", () => {
  it("uses the local calendar date", () => {
    expect(todayIsoDate(new Date(2026, 0, 5, 23, 59))).toBe("2026-01-05");
  });
});
