import { execFileSync } from "node:child_process";
import { rmSync } from "node:fs";
import path from "node:path";

const apiRoot = path.join(import.meta.dirname, "..");
const testDbPath = path.join(apiRoot, "prisma", "test.db");

// Recreate the test database from the migrations once per test run.
export default function setup() {
  rmSync(testDbPath, { force: true });
  execFileSync("npx", ["prisma", "migrate", "deploy"], {
    cwd: apiRoot,
    env: { ...process.env, DATABASE_URL: `file:${testDbPath}` },
    stdio: "pipe",
  });
}
