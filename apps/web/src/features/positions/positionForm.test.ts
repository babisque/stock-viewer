import { describe, expect, it } from "vitest";
import {
  emptyFormValues,
  formValuesFromPosition,
  normalizeFormValues,
  positionFormSchema,
} from "./positionForm";

describe("positionFormSchema", () => {
  it("accepts comma decimals and outputs dot-decimal strings", () => {
    const parsed = positionFormSchema.parse({
      ...emptyFormValues,
      ticker: "petr4",
      quantity: "100",
      averagePrice: "28,45",
    });
    expect(parsed).toEqual({
      ticker: "PETR4",
      market: "B3",
      quantity: "100",
      averagePrice: "28.45",
      notes: null,
    });
  });

  it("rejects invalid values", () => {
    const result = positionFormSchema.safeParse({
      ...emptyFormValues,
      quantity: "-1",
    });
    expect(result.success).toBe(false);
    const paths = result.error?.issues.map((issue) => issue.path[0]);
    expect(paths).toEqual(
      expect.arrayContaining(["ticker", "quantity", "averagePrice"]),
    );
  });
});

describe("normalizeFormValues", () => {
  it("only touches the decimal fields", () => {
    expect(
      normalizeFormValues({
        ...emptyFormValues,
        ticker: "A,B",
        quantity: "1,5",
        averagePrice: "2",
      }),
    ).toEqual({
      ...emptyFormValues,
      ticker: "A,B",
      quantity: "1.5",
      averagePrice: "2",
    });
  });
});

describe("formValuesFromPosition", () => {
  it("maps a position to form values with empty notes", () => {
    expect(
      formValuesFromPosition({
        id: "1",
        ticker: "AAPL",
        market: "US",
        currency: "USD",
        quantity: "1.5",
        averagePrice: "190.2",
        notes: null,
        createdAt: "",
        updatedAt: "",
      }),
    ).toEqual({
      ticker: "AAPL",
      market: "US",
      quantity: "1.5",
      averagePrice: "190.2",
      notes: "",
    });
  });
});
