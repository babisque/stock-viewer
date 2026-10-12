import { FxRateSchema, PortfolioSchema } from "@stock-viewer/shared";
import type { FastifyPluginAsyncZod } from "fastify-type-provider-zod";
import type { FxService } from "../fx/usd-brl";
import { appError } from "../lib/errors";
import type { PrismaClient } from "../lib/prisma";
import { getPortfolio } from "../portfolio/build-portfolio";
import type { QuoteService } from "../prices/get-quotes";

type PortfolioRouteDeps = {
  prisma: PrismaClient;
  quotes: QuoteService;
  fx: FxService;
};

export function portfolioRoutes(
  deps: PortfolioRouteDeps,
): FastifyPluginAsyncZod {
  return async (app) => {
    app.get(
      "/portfolio",
      { schema: { response: { 200: PortfolioSchema } } },
      async () => getPortfolio(deps),
    );

    app.post(
      "/portfolio/refresh",
      { schema: { response: { 200: PortfolioSchema } } },
      async () => {
        deps.quotes.clear();
        deps.fx.clear();
        return getPortfolio(deps);
      },
    );

    app.get(
      "/fx/usd-brl",
      { schema: { response: { 200: FxRateSchema } } },
      async () => {
        const { fx } = await deps.fx.getRate();
        if (!fx) {
          throw appError(503, "FX_UNAVAILABLE", "USD/BRL rate is unavailable");
        }
        return fx;
      },
    );
  };
}
