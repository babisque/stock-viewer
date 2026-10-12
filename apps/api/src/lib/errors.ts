import type { ErrorCode } from "@stock-viewer/shared";

export type AppError = Error & { statusCode: number; code: ErrorCode };

export function appError(
  statusCode: number,
  code: ErrorCode,
  message: string,
): AppError {
  return Object.assign(new Error(message), {
    statusCode,
    code,
    isAppError: true,
  });
}

export function isAppError(error: unknown): error is AppError {
  return (
    error instanceof Error && "isAppError" in error && error.isAppError === true
  );
}

export function errorBody(code: ErrorCode, message: string) {
  return { error: { code, message } };
}

// Prisma's "known request error" codes, without importing the runtime classes.
export function hasPrismaCode(error: unknown, code: string): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    error.code === code
  );
}
