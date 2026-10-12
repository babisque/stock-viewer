import Decimal from "decimal.js";
import { z } from "zod";

const DECIMAL_PATTERN = /^\d+(\.\d+)?$/;

// SQLite (via Prisma) stores Decimal columns as 64-bit floats, which only keep
// ~15 significant digits. Reject longer values instead of silently rounding them.
export const MAX_SIGNIFICANT_DIGITS = 15;

export function countDecimals(value: string): number {
  const fraction = value.split(".")[1];
  return fraction ? fraction.length : 0;
}

// A well-formed, non-negative decimal string within the precision limits.
// `requirement` completes the "must be ..." message for malformed input.
function decimalString(label: string, requirement: string) {
  return (
    z
      .string({ error: `${label} is required` })
      .trim()
      // abort: later checks only run on a well-formed decimal string
      .min(1, { message: `${label} is required`, abort: true })
      .regex(DECIMAL_PATTERN, {
        message: `${label} must be ${requirement}`,
        abort: true,
      })
  );
}

function withPrecisionLimits(
  schema: z.ZodString,
  label: string,
  maxDecimals: number,
) {
  return schema
    .refine(
      (value) => countDecimals(value) <= maxDecimals,
      `${label} can have at most ${maxDecimals} decimal places`,
    )
    .refine(
      (value) => new Decimal(value).precision() <= MAX_SIGNIFICANT_DIGITS,
      `${label} can have at most ${MAX_SIGNIFICANT_DIGITS} significant digits`,
    );
}

// A positive decimal number serialized as a string ("28.45"). Never a JS number.
export function positiveDecimalString(label: string, maxDecimals: number) {
  return withPrecisionLimits(
    decimalString(label, "a positive number").refine(
      (value) => new Decimal(value).greaterThan(0),
      `${label} must be greater than 0`,
    ),
    label,
    maxDecimals,
  );
}

// Zero or a positive decimal string, e.g. brokerage fees ("0", "4.90").
export function nonNegativeDecimalString(label: string, maxDecimals: number) {
  return withPrecisionLimits(
    decimalString(label, "zero or a positive number"),
    label,
    maxDecimals,
  );
}
