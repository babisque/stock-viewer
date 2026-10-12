import Fastify, { type FastifyServerOptions } from "fastify";
import {
  hasZodFastifySchemaValidationErrors,
  serializerCompiler,
  validatorCompiler,
  type ZodTypeProvider,
} from "fastify-type-provider-zod";
import type { FxService } from "./fx/usd-brl";
import { errorBody, isAppError } from "./lib/errors";
import type { PrismaClient } from "./lib/prisma";
import type { QuoteService } from "./prices/get-quotes";
import { healthRoutes } from "./routes/health";
import { portfolioRoutes } from "./routes/portfolio";
import { positionRoutes } from "./routes/positions";
import { transactionRoutes } from "./routes/transactions";

type BuildAppOptions = {
  prisma: PrismaClient;
  quotes: QuoteService;
  fx: FxService;
  logger?: FastifyServerOptions["logger"];
};

export function buildApp({
  prisma,
  quotes,
  fx,
  logger = false,
}: BuildAppOptions) {
  const app = Fastify({ logger }).withTypeProvider<ZodTypeProvider>();

  app.setValidatorCompiler(validatorCompiler);
  app.setSerializerCompiler(serializerCompiler);

  app.setErrorHandler((error, request, reply) => {
    if (hasZodFastifySchemaValidationErrors(error)) {
      const issue = error.validation[0];
      if (!issue)
        return reply
          .code(400)
          .send(errorBody("VALIDATION_ERROR", error.message));
      const path = issue.instancePath.replace(/^\//, "").replaceAll("/", ".");
      const issueMessage = issue.message ?? error.message;
      const message = path ? `${path}: ${issueMessage}` : issueMessage;
      return reply.code(400).send(errorBody("VALIDATION_ERROR", message));
    }
    if (isAppError(error)) {
      return reply
        .code(error.statusCode)
        .send(errorBody(error.code, error.message));
    }
    // Malformed JSON and similar client errors raised by Fastify itself.
    if (
      error instanceof Error &&
      "statusCode" in error &&
      error.statusCode === 400
    ) {
      return reply.code(400).send(errorBody("VALIDATION_ERROR", error.message));
    }
    request.log.error(error);
    return reply
      .code(500)
      .send(errorBody("INTERNAL_ERROR", "Something went wrong"));
  });

  app.setNotFoundHandler((request, reply) =>
    reply
      .code(404)
      .send(
        errorBody(
          "NOT_FOUND",
          `Route ${request.method} ${request.url} not found`,
        ),
      ),
  );

  app.register(healthRoutes, { prefix: "/api" });
  app.register(positionRoutes(prisma), { prefix: "/api" });
  app.register(transactionRoutes(prisma), { prefix: "/api" });
  app.register(portfolioRoutes({ prisma, quotes, fx }), { prefix: "/api" });

  return app;
}
