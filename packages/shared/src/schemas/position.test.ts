import { describe, expect, it } from "vitest";
import { CreatePositionSchema, UpdatePositionSchema } from "./position";

const valid = {
  ticker: "PETR4",
  market: "B3",
  quantity: "100",
  averagePrice: "28.45",
};

function firstIssue(input: unknown) {
  const result = CreatePositionSchema.safeParse(input);
  expect(result.success).toBe(false);
  return result.error?.issues[0];
}

describe("CreatePositionSchema", () => {
  it("accepts a valid position and defaults notes to null", () => {
    expect(CreatePositionSchema.parse(valid)).toEqual({
      ...valid,
      notes: null,
    });
  });

  it("normalizes the ticker with trim + uppercase", () => {
    expect(
      CreatePositionSchema.parse({ ...valid, ticker: "  petr4 " }).ticker,
    ).toBe("PETR4");
  });

  it("accepts tickers with dots like BRK.B", () => {
    expect(
      CreatePositionSchema.parse({ ...valid, ticker: "brk.b", market: "US" })
        .ticker,
    ).toBe("BRK.B");
  });

  it("rejects an empty ticker", () => {
    expect(firstIssue({ ...valid, ticker: "  " })?.message).toBe(
      "Ticker is required",
    );
  });

  it("rejects a .SA suffix", () => {
    expect(firstIssue({ ...valid, ticker: "petr4.sa" })?.message).toBe(
      "Enter the ticker without the .SA suffix",
    );
  });

  it("rejects tickers with invalid characters", () => {
    expect(firstIssue({ ...valid, ticker: "PETR 4" })?.path).toEqual([
      "ticker",
    ]);
  });

  it("rejects an unknown market", () => {
    expect(firstIssue({ ...valid, market: "NYSE" })?.path).toEqual(["market"]);
  });

  it("rejects negative and zero quantity", () => {
    expect(firstIssue({ ...valid, quantity: "-10" })?.path).toEqual([
      "quantity",
    ]);
    expect(firstIssue({ ...valid, quantity: "0" })?.path).toEqual(["quantity"]);
  });

  it("rejects fractional quantity on B3", () => {
    expect(firstIssue({ ...valid, quantity: "10.5" })).toMatchObject({
      path: ["quantity"],
      message: "B3 quantity must be a whole number",
    });
  });

  it("accepts fractional quantity on US up to 8 decimals", () => {
    const parsed = CreatePositionSchema.parse({
      ...valid,
      market: "US",
      quantity: "0.12345678",
    });
    expect(parsed.quantity).toBe("0.12345678");
    expect(
      firstIssue({ ...valid, market: "US", quantity: "0.123456789" })?.path,
    ).toEqual(["quantity"]);
  });

  it("rejects a non-positive average price", () => {
    expect(firstIssue({ ...valid, averagePrice: "0" })?.path).toEqual([
      "averagePrice",
    ]);
  });

  it("trims notes and turns empty notes into null", () => {
    expect(
      CreatePositionSchema.parse({ ...valid, notes: "  long term " }).notes,
    ).toBe("long term");
    expect(
      CreatePositionSchema.parse({ ...valid, notes: "   " }).notes,
    ).toBeNull();
  });

  it("rejects notes longer than 500 characters", () => {
    expect(firstIssue({ ...valid, notes: "x".repeat(501) })?.path).toEqual([
      "notes",
    ]);
  });
});

describe("UpdatePositionSchema", () => {
  it("only keeps editable fields", () => {
    const parsed = UpdatePositionSchema.parse({
      ticker: "OTHER",
      quantity: "5",
      averagePrice: "10",
    });
    expect(parsed).toEqual({ quantity: "5", averagePrice: "10", notes: null });
  });

  it("allows updating only the notes", () => {
    expect(UpdatePositionSchema.parse({ notes: "hold" })).toEqual({
      notes: "hold",
    });
  });
});
