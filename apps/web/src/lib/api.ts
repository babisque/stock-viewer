import { ApiErrorBodySchema, type ErrorCode } from "@stock-viewer/shared";
import type { z } from "zod";

export type ApiError = Error & {
  status: number;
  code: ErrorCode | "NETWORK_ERROR";
};

export function apiError(
  status: number,
  code: ApiError["code"],
  message: string,
): ApiError {
  return Object.assign(new Error(message), { status, code, isApiError: true });
}

export function isApiError(error: unknown): error is ApiError {
  return (
    error instanceof Error && "isApiError" in error && error.isApiError === true
  );
}

async function toApiError(response: Response): Promise<ApiError> {
  const body: unknown = await response.json().catch(() => null);
  const parsed = ApiErrorBodySchema.safeParse(body);
  if (parsed.success) {
    return apiError(
      response.status,
      parsed.data.error.code,
      parsed.data.error.message,
    );
  }
  return apiError(
    response.status,
    "INTERNAL_ERROR",
    `Request failed (${response.status})`,
  );
}

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
};

// Calls the API and validates the JSON response with a shared Zod schema.
// Pass `null` as schema for endpoints without a response body (204).
export async function apiFetch<T extends z.ZodType>(
  path: string,
  schema: T,
  options?: RequestOptions,
): Promise<z.infer<T>>;
export async function apiFetch(
  path: string,
  schema: null,
  options?: RequestOptions,
): Promise<void>;
export async function apiFetch(
  path: string,
  schema: z.ZodType | null,
  { method = "GET", body }: RequestOptions = {},
): Promise<unknown> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers:
        body === undefined ? undefined : { "content-type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw apiError(0, "NETWORK_ERROR", "Could not reach the server");
  }

  if (!response.ok) throw await toApiError(response);
  if (schema === null) return undefined;
  return schema.parse(await response.json());
}
