import {
  CreateTransactionSchema,
  PositionIdParamsSchema,
  PositionSchema,
  TransactionIdParamsSchema,
  TransactionSchema,
} from "@stock-viewer/shared";
import type { FastifyPluginAsyncZod } from "fastify-type-provider-zod";
import { z } from "zod";
import type { PrismaClient } from "../lib/prisma";
import {
  createTransaction,
  deleteTransaction,
  listTransactions,
} from "../transactions/transactions";

export function transactionRoutes(prisma: PrismaClient): FastifyPluginAsyncZod {
  return async (app) => {
    app.get(
      "/positions/:id/transactions",
      {
        schema: {
          params: PositionIdParamsSchema,
          response: { 200: z.array(TransactionSchema) },
        },
      },
      async (request) => listTransactions(prisma, request.params.id),
    );

    app.post(
      "/positions/:id/transactions",
      {
        schema: {
          params: PositionIdParamsSchema,
          body: CreateTransactionSchema,
          response: { 201: PositionSchema },
        },
      },
      async (request, reply) => {
        const position = await createTransaction(
          prisma,
          request.params.id,
          request.body,
        );
        return reply.code(201).send(position);
      },
    );

    app.delete(
      "/transactions/:id",
      { schema: { params: TransactionIdParamsSchema } },
      async (request, reply) => {
        await deleteTransaction(prisma, request.params.id);
        return reply.code(204).send();
      },
    );
  };
}
