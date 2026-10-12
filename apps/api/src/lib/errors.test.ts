import { describe, expect, it } from "vitest";
import { appError, errorBody, hasPrismaCode, isAppError } from "./errors";

describe("appError", () => {
  it("creates an Error carrying status and code", () => {
    const error = appError(404, "POSITION_NOT_FOUND", "missing");
    expect(error).toBeInstanceOf(Error);
    expect(error).toMatchObject({
      statusCode: 404,
      code: "POSITION_NOT_FOUND",
      message: "missing",
    });
    expect(isAppError(error)).toBe(true);
  });

  it("does not treat plain errors as app errors", () => {
    expect(isAppError(new Error("boom"))).toBe(false);
    expect(isAppError({ isAppError: true })).toBe(false);
  });
});

describe("errorBody", () => {
  it("uses the single error shape", () => {
    expect(errorBody("DUPLICATE_POSITION", "dup")).toEqual({
      error: { code: "DUPLICATE_POSITION", message: "dup" },
    });
  });
});

describe("hasPrismaCode", () => {
  it("matches objects with the given code", () => {
    expect(hasPrismaCode({ code: "P2002" }, "P2002")).toBe(true);
    expect(hasPrismaCode({ code: "P2025" }, "P2002")).toBe(false);
    expect(hasPrismaCode(null, "P2002")).toBe(false);
  });
});
