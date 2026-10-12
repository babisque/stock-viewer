import path from "node:path";
import { PrismaBetterSqlite3 } from "@prisma/adapter-better-sqlite3";
import { PrismaClient } from "../generated/prisma/client";

const defaultDatabaseUrl = `file:${path.join(import.meta.dirname, "..", "..", "prisma", "dev.db")}`;

export function createPrismaClient(
  databaseUrl = process.env.DATABASE_URL ?? defaultDatabaseUrl,
) {
  return new PrismaClient({
    adapter: new PrismaBetterSqlite3({ url: databaseUrl }),
  });
}

export type { PrismaClient };
