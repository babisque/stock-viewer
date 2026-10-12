import { describe, expect, it } from "vitest";
import {
  allocationSlices,
  groupOther,
  type AllocationItem,
} from "./allocation";

function summary(slices: ReturnType<typeof allocationSlices>) {
  return slices.map((slice) => [
    slice.key,
    slice.value.toString(),
    slice.percent.toString(),
  ]);
}

describe("allocationSlices", () => {
  const items: AllocationItem[] = [
    { key: "B3", label: "B3", currency: "BRL", value: "1500" },
    { key: "US", label: "US", currency: "USD", value: "100" },
    { key: "B3", label: "B3", currency: "BRL", value: "1000" },
  ];

  it("groups by key and converts to the target currency, largest first", () => {
    expect(summary(allocationSlices(items, "BRL", "5"))).toEqual([
      ["B3", "2500", "83.333333333333333333"],
      ["US", "500", "16.666666666666666667"],
    ]);
  });

  it("gives the same percentages in either currency", () => {
    expect(summary(allocationSlices(items, "USD", "5"))).toEqual([
      ["B3", "500", "83.333333333333333333"],
      ["US", "100", "16.666666666666666667"],
    ]);
  });

  it("leaves out items without a value", () => {
    const slices = allocationSlices(
      [
        { key: "PETR4", label: "PETR4", currency: "BRL", value: "100" },
        { key: "XXXX3", label: "XXXX3", currency: "BRL", value: null },
      ],
      "BRL",
      "5",
    );
    expect(summary(slices)).toEqual([["PETR4", "100", "100"]]);
  });

  it("keeps full precision", () => {
    const slices = allocationSlices(
      [
        { key: "a", label: "a", currency: "BRL", value: "0.1" },
        { key: "b", label: "b", currency: "BRL", value: "0.2" },
      ],
      "BRL",
      "5",
    );
    expect(slices.map((slice) => slice.value.toString())).toEqual([
      "0.2",
      "0.1",
    ]);
    expect(slices[0]!.percent.plus(slices[1]!.percent).toString()).toBe("100");
  });

  it("returns nothing for an empty or zero-value portfolio", () => {
    expect(allocationSlices([], "BRL", "5")).toEqual([]);
    expect(
      allocationSlices(
        [{ key: "a", label: "a", currency: "BRL", value: "0" }],
        "BRL",
        "5",
      ),
    ).toEqual([]);
  });
});

describe("groupOther", () => {
  const slices = allocationSlices(
    ["50", "20", "15", "10", "5"].map((value, index) => ({
      key: `t${index}`,
      label: `T${index}`,
      currency: "BRL" as const,
      value,
    })),
    "BRL",
    "5",
  );

  it("returns the slices unchanged when within the limit", () => {
    expect(groupOther(slices, 5)).toBe(slices);
  });

  it("merges the smallest slices into Other", () => {
    expect(summary(groupOther(slices, 3))).toEqual([
      ["t0", "50", "50"],
      ["t1", "20", "20"],
      ["other", "30", "30"],
    ]);
    expect(groupOther(slices, 3)[2]!.label).toBe("Other");
  });
});
