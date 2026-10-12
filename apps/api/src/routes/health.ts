import { HealthSchema } from "@stock-viewer/shared";
import type { FastifyPluginAsyncZod } from "fastify-type-provider-zod";

export const healthRoutes: FastifyPluginAsyncZod = async (app) => {
  app.get(
    "/health",
    { schema: { response: { 200: HealthSchema } } },
    async () => ({
      status: "ok" as const,
    }),
  );
};
