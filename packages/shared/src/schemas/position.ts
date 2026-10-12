import Decimal from "decimal.js";
import { z } from "zod";
import { CurrencySchema, MarketSchema, type Market } from "./market";
import { positiveDecimalString } from "./decimal";

export const MAX_DECIMALS = 8;
export const NOTES_MAX_LENGTH = 500;

export const TickerSchema = z
  .string({ error: "Ticker is required" })
  .trim()
  .toUpperCase()
  .min(1, "Ticker is required")
  .refine(
    (value) => !value.endsWith(".SA"),
    "Enter the ticker without the .SA suffix",
  )
  .refine(
    (value) => /^[A-Z0-9][A-Z0-9.-]{0,11}$/.test(value),
    "Ticker may only contain letters, digits, '.' and '-' (max 12 characters)",
  );

export const QuantitySchema = positiveDecimalString("Quantity", MAX_DECIMALS);
export const AveragePriceSchema = positiveDecimalString(
  "Average price",
  MAX_DECIMALS,
);

export const NotesSchema = z
  .string()
  .trim()
  .max(
    NOTES_MAX_LENGTH,
    `Notes can have at most ${NOTES_MAX_LENGTH} characters`,
  )
  .nullish()
  .transform((value) => (value ? value : null));

// B3 shares are traded in whole units; US brokers allow fractional shares.
export function isValidQuantityForMarket(
  quantity: string,
  market: Market,
): boolean {
  return market === "US" || new Decimal(quantity).isInteger();
}

export const B3_WHOLE_QUANTITY_MESSAGE = "B3 quantity must be a whole number";

export const CreatePositionSchema = z
  .object({
    ticker: TickerSchema,
    market: MarketSchema,
    quantity: QuantitySchema,
    averagePrice: AveragePriceSchema,
    notes: NotesSchema,
  })
  .superRefine((value, ctx) => {
    if (!isValidQuantityForMarket(value.quantity, value.market)) {
      ctx.addIssue({
        code: "custom",
        path: ["quantity"],
        message: B3_WHOLE_QUANTITY_MESSAGE,
      });
    }
  });
export type CreatePosition = z.infer<typeof CreatePositionSchema>;
export type CreatePositionInput = z.input<typeof CreatePositionSchema>;

// Ticker and market are immutable after creation. Quantity and average price
// are derived from the transactions: they are optional, and the API rejects
// values that differ from the derived ones.
export const UpdatePositionSchema = z.object({
  quantity: QuantitySchema.optional(),
  averagePrice: AveragePriceSchema.optional(),
  notes: NotesSchema,
});
export type UpdatePosition = z.infer<typeof UpdatePositionSchema>;
export type UpdatePositionInput = z.input<typeof UpdatePositionSchema>;

export const PositionSchema = z.object({
  id: z.string(),
  ticker: z.string(),
  market: MarketSchema,
  currency: CurrencySchema,
  quantity: z.string(),
  averagePrice: z.string(),
  notes: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type Position = z.infer<typeof PositionSchema>;

export const PositionIdParamsSchema = z.object({ id: z.string().min(1) });
export type PositionIdParams = z.infer<typeof PositionIdParamsSchema>;
