import YahooFinance from "yahoo-finance2";
import { buildApp } from "./app";
import { createFxService } from "./fx/usd-brl";
import { createPrismaClient } from "./lib/prisma";
import { createBrapiProvider } from "./prices/brapi";
import { createQuoteService } from "./prices/get-quotes";
import type { Logger } from "./prices/types";
import { createYahooProvider } from "./prices/yahoo";

const prisma = createPrismaClient();
const yahooFinance = new YahooFinance({ suppressNotices: ["yahooSurvey"] });

// Services log through the Fastify logger; `app` is only read when a log
// call happens, which is after it has been created.
const log: Logger = {
  warn: (details, message) => app.log.warn(details, message),
};

const quotes = createQuoteService({
  providers: [
    createYahooProvider({ quote: (symbols) => yahooFinance.quote(symbols) }),
    createBrapiProvider({ token: process.env.BRAPI_TOKEN || undefined }),
  ],
  log,
});
const fx = createFxService({ log });

const app = buildApp({ prisma, quotes, fx, logger: true });

const port = Number(process.env.PORT ?? 3001);

try {
  await app.listen({ port, host: "127.0.0.1" });
} catch (error) {
  app.log.error(error);
  await prisma.$disconnect();
  process.exit(1);
}
