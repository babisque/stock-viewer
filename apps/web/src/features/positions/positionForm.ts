import {
  CreatePositionSchema,
  MarketSchema,
  normalizeDecimalInput,
  NotesSchema,
  type Market,
  type Position,
} from "@stock-viewer/shared";
import { z } from "zod";

// Raw values as typed in the form (everything is text, decimals may use a comma).
export type PositionFormValues = {
  ticker: string;
  market: Market;
  quantity: string;
  averagePrice: string;
  notes: string;
};

export const emptyFormValues: PositionFormValues = {
  ticker: "",
  market: "B3",
  quantity: "",
  averagePrice: "",
  notes: "",
};

export function formValuesFromPosition(position: Position): PositionFormValues {
  return {
    ticker: position.ticker,
    market: position.market,
    quantity: position.quantity,
    averagePrice: position.averagePrice,
    notes: position.notes ?? "",
  };
}

export function normalizeFormValues(
  values: PositionFormValues,
): PositionFormValues {
  return {
    ...values,
    quantity: normalizeDecimalInput(values.quantity),
    averagePrice: normalizeDecimalInput(values.averagePrice),
  };
}

// The shared create schema, applied after accepting "28,45" as "28.45".
export const positionFormSchema = z.preprocess(
  (values: PositionFormValues) => normalizeFormValues(values),
  CreatePositionSchema,
);

// When editing, only the notes can change: ticker and market are fixed, and
// quantity and average price are derived from the transactions (they may be
// 0 or have more decimals than a user could type), so they pass unchecked.
export const editPositionFormSchema = z.preprocess(
  (values: PositionFormValues) => values,
  z.object({
    ticker: z.string(),
    market: MarketSchema,
    quantity: z.string(),
    averagePrice: z.string(),
    notes: NotesSchema,
  }),
);
