import {
  CreatePositionSchema,
  PositionIdParamsSchema,
  PositionSchema,
  UpdatePositionSchema,
} from "@stock-viewer/shared";
import type { FastifyPluginAsyncZod } from "fastify-type-provider-zod";
import { z } from "zod";
import type { PrismaClient } from "../lib/prisma";
import {
  createPosition,
  deletePosition,
  listPositions,
  updatePosition,
} from "../positions/positions";

export function positionRoutes(prisma: PrismaClient): FastifyPluginAsyncZod {
  return async (app) => {
    app.get(
      "/positions",
      { schema: { response: { 200: z.array(PositionSchema) } } },
      async () => listPositions(prisma),
    );

    app.post(
      "/positions",
      {
        schema: {
          body: CreatePositionSchema,
          response: { 201: PositionSchema },
        },
      },
      async (request, reply) => {
        const position = await createPosition(prisma, request.body);
        return reply.code(201).send(position);
      },
    );

    app.put(
      "/positions/:id",
      {
        schema: {
          params: PositionIdParamsSchema,
          body: UpdatePositionSchema,
          response: { 200: PositionSchema },
        },
      },
      async (request) =>
        updatePosition(prisma, request.params.id, request.body),
    );

    app.delete(
      "/positions/:id",
      { schema: { params: PositionIdParamsSchema } },
      async (request, reply) => {
        await deletePosition(prisma, request.params.id);
        return reply.code(204).send();
      },
    );
  };
}
