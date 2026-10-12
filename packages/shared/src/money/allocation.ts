import Decimal from "decimal.js";
import type { Currency } from "../schemas/market";
import { convert } from "./convert";

export type AllocationItem = {
  key: string;
  label: string;
  currency: Currency;
  value: Decimal | string | null; // null = no price, left out
};

export type AllocationSlice = {
  key: string;
  label: string;
  value: Decimal; // in the target currency
  percent: Decimal; // 0–100 of the total
};

export const OTHER_KEY = "other";

function withPercents(
  groups: { key: string; label: string; value: Decimal }[],
): AllocationSlice[] {
  const total = groups.reduce(
    (sum, group) => sum.plus(group.value),
    new Decimal(0),
  );
  if (total.isZero()) return [];
  return groups
    .map((group) => ({ ...group, percent: group.value.div(total).times(100) }))
    .sort((a, b) => b.value.comparedTo(a.value));
}

// Groups items by key (e.g. by market), converts every value to `target`
// with the current rate, and returns each group's share of the total,
// largest first. Items without a value are skipped.
export function allocationSlices(
  items: AllocationItem[],
  target: Currency,
  usdBrlRate: Decimal | string,
): AllocationSlice[] {
  const groups = new Map<
    string,
    { key: string; label: string; value: Decimal }
  >();
  for (const item of items) {
    if (item.value === null) continue;
    const value = convert(item.value, item.currency, target, usdBrlRate);
    const group = groups.get(item.key);
    if (group) {
      group.value = group.value.plus(value);
    } else {
      groups.set(item.key, { key: item.key, label: item.label, value });
    }
  }
  return withPercents([...groups.values()]);
}

// Keeps the largest `maxSlices - 1` slices and merges the rest into "Other",
// so a chart never has more than `maxSlices` slices.
export function groupOther(
  slices: AllocationSlice[],
  maxSlices: number,
): AllocationSlice[] {
  if (slices.length <= maxSlices) return slices;
  const kept = slices.slice(0, maxSlices - 1);
  const rest = slices.slice(maxSlices - 1);
  const other: AllocationSlice = {
    key: OTHER_KEY,
    label: "Other",
    value: rest.reduce((sum, slice) => sum.plus(slice.value), new Decimal(0)),
    percent: rest.reduce(
      (sum, slice) => sum.plus(slice.percent),
      new Decimal(0),
    ),
  };
  return [...kept, other];
}
