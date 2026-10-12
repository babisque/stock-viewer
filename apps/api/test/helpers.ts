import { afterAll, beforeEach } from "vitest";
import { buildApp } from "../src/app";
import { createFxService, type FxService } from "../src/fx/usd-brl";
import { createPrismaClient } from "../src/lib/prisma";
import {
  createQuoteService,
  type QuoteService,
} from "../src/prices/get-quotes";
import { fakeLogger } from "./fakes";

type TestServices = { quotes?: QuoteService; fx?: FxService };

// Shared test context: a Prisma client on the test DB (DATABASE_URL is set by
// vitest.config.ts) and a Fastify app, with tables wiped before every test.
// Price services default to no providers; FX requests go through msw.
export function setupTestContext(services: TestServices = {}) {
  const prisma = createPrismaClient();
  const app = buildApp({
    prisma,
    quotes:
      services.quotes ??
      createQuoteService({ providers: [], log: fakeLogger() }),
    fx: services.fx ?? createFxService({ log: fakeLogger() }),
  });

  beforeEach(async () => {
    await prisma.transaction.deleteMany();
    await prisma.position.deleteMany();
  });

  afterAll(async () => {
    await app.close();
    await prisma.$disconnect();
  });

  return { prisma, app };
}
