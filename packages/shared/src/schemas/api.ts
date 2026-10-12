import { z } from "zod";

export const ErrorCodeSchema = z.enum([
  "VALIDATION_ERROR",
  "POSITION_NOT_FOUND",
  "DUPLICATE_POSITION",
  "NOT_FOUND",
  "INTERNAL_ERROR",
  "FX_UNAVAILABLE",
  "TRANSACTION_NOT_FOUND",
  "INSUFFICIENT_QUANTITY",
  "DERIVED_VALUE_READ_ONLY",
]);
export type ErrorCode = z.infer<typeof ErrorCodeSchema>;

export const ApiErrorBodySchema = z.object({
  error: z.object({
    code: ErrorCodeSchema,
    message: z.string(),
  }),
});
export type ApiErrorBody = z.infer<typeof ApiErrorBodySchema>;

export const HealthSchema = z.object({ status: z.literal("ok") });
export type Health = z.infer<typeof HealthSchema>;
