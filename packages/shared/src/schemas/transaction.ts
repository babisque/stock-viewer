import { z } from "zod";
import { nonNegativeDecimalString, positiveDecimalString } from "./decimal";
import { CurrencySchema } from "./market";
import { MAX_DECIMALS, QuantitySchema } from "./position";

export const TransactionTypeSchema = z.enum(["BUY", "SELL"]);
export type TransactionType = z.infer<typeof TransactionTypeSchema>;

const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

// Today's calendar date ("2026-10-12") in the local time zone. The API runs
// on the user's own machine, so server and browser agree on "today".
export function todayIsoDate(now: Date = new Date()): string {
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function isCalendarDate(value: string): boolean {
  const date = new Date(`${value}T00:00:00.000Z`);
  return (
    !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value
  );
}

// A trade date without time. ISO dates compare correctly as strings.
export const TransactionDateSchema = z
  .string({ error: "Date is required" })
  .trim()
  .min(1, { message: "Date is required", abort: true })
  .regex(ISO_DATE_PATTERN, {
    message: "Date must be in YYYY-MM-DD format",
    abort: true,
  })
  .refine(isCalendarDate, { message: "Date is not a valid date", abort: true })
  .refine((value) => value <= todayIsoDate(), "Date can't be in the future");

export const UnitPriceSchema = positiveDecimalString(
  "Unit price",
  MAX_DECIMALS,
);
export const FeesSchema = nonNegativeDecimalString("Fees", MAX_DECIMALS);

// The B3 whole-quantity rule depends on the position's market, so the API
// checks it after loading the position.
export const CreateTransactionSchema = z.object({
  type: TransactionTypeSchema,
  date: TransactionDateSchema,
  quantity: QuantitySchema,
  unitPrice: UnitPriceSchema,
  fees: FeesSchema.default("0"),
});
export type CreateTransaction = z.infer<typeof CreateTransactionSchema>;
export type CreateTransactionInput = z.input<typeof CreateTransactionSchema>;

export const TransactionSchema = z.object({
  id: z.string(),
  positionId: z.string(),
  type: TransactionTypeSchema,
  date: z.string(), // YYYY-MM-DD
  quantity: z.string(),
  unitPrice: z.string(), // in the position's currency
  fees: z.string(), // in the position's currency
  currency: CurrencySchema,
  createdAt: z.string(), // ISO 8601
});
export type Transaction = z.infer<typeof TransactionSchema>;

export const TransactionIdParamsSchema = z.object({ id: z.string().min(1) });
export type TransactionIdParams = z.infer<typeof TransactionIdParamsSchema>;
