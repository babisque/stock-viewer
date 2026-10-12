import path from "node:path";
import { defineProject } from "vitest/config";

const testDatabaseUrl = `file:${path.join(import.meta.dirname, "prisma", "test.db")}`;

export default defineProject({
  test: {
    name: "api",
    environment: "node",
    globalSetup: ["./test/global-setup.ts"],
    setupFiles: ["./test/setup.ts"],
    // msw touches localStorage, which Node only warns about; the API never
    // needs Web Storage.
    execArgv: ["--no-experimental-webstorage"],
    env: { DATABASE_URL: testDatabaseUrl },
    // All test files share one SQLite test database.
    fileParallelism: false,
  },
});
