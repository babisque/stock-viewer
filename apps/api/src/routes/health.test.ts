import { describe, expect, it } from "vitest";
import { setupTestContext } from "../../test/helpers";

const { app } = setupTestContext();

describe("GET /api/health", () => {
  it("returns ok", async () => {
    const response = await app.inject({ method: "GET", url: "/api/health" });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({ status: "ok" });
  });

  it("uses the error shape for unknown routes", async () => {
    const response = await app.inject({ method: "GET", url: "/api/nope" });
    expect(response.statusCode).toBe(404);
    expect(response.json()).toEqual({
      error: { code: "NOT_FOUND", message: "Route GET /api/nope not found" },
    });
  });
});
