import { PositionSchema } from "@stock-viewer/shared";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch, isApiError } from "./api";

const position = {
  id: "1",
  ticker: "PETR4",
  market: "B3",
  currency: "BRL",
  quantity: "100",
  averagePrice: "28.45",
  notes: null,
  createdAt: "2026-10-11T00:00:00.000Z",
  updatedAt: "2026-10-11T00:00:00.000Z",
};

function mockFetch(response: Response | Error) {
  const fn = vi.fn(() =>
    response instanceof Error
      ? Promise.reject(response)
      : Promise.resolve(response),
  );
  vi.stubGlobal("fetch", fn);
  return fn;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("apiFetch", () => {
  it("prefixes /api, sends JSON and parses the response with the schema", async () => {
    const fetchMock = mockFetch(Response.json(position, { status: 201 }));
    const result = await apiFetch("/positions", PositionSchema, {
      method: "POST",
      body: { ticker: "PETR4" },
    });
    expect(result).toEqual(position);
    expect(fetchMock).toHaveBeenCalledWith("/api/positions", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ ticker: "PETR4" }),
    });
  });

  it("returns undefined for body-less responses", async () => {
    mockFetch(new Response(null, { status: 204 }));
    await expect(
      apiFetch("/positions/1", null, { method: "DELETE" }),
    ).resolves.toBeUndefined();
  });

  it("throws an ApiError built from the error shape", async () => {
    mockFetch(
      Response.json(
        { error: { code: "DUPLICATE_POSITION", message: "already exists" } },
        { status: 409 },
      ),
    );
    const error = await apiFetch("/positions", PositionSchema).catch(
      (e: unknown) => e,
    );
    expect(isApiError(error)).toBe(true);
    expect(error).toMatchObject({
      status: 409,
      code: "DUPLICATE_POSITION",
      message: "already exists",
    });
  });

  it("falls back to a generic error for unexpected error bodies", async () => {
    mockFetch(new Response("oops", { status: 502 }));
    await expect(apiFetch("/positions", PositionSchema)).rejects.toMatchObject({
      status: 502,
      code: "INTERNAL_ERROR",
    });
  });

  it("reports network failures", async () => {
    mockFetch(new TypeError("fetch failed"));
    await expect(apiFetch("/positions", PositionSchema)).rejects.toMatchObject({
      code: "NETWORK_ERROR",
    });
  });
});
