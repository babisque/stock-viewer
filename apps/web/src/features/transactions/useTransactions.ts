import {
  PositionSchema,
  TransactionSchema,
  type CreateTransaction,
  type Transaction,
} from "@stock-viewer/shared";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { portfolioKey } from "@/features/dashboard/usePortfolio";
import { positionsKey } from "@/features/positions/usePositions";
import { apiFetch } from "@/lib/api";

export function transactionsKey(positionId: string) {
  return ["transactions", positionId] as const;
}

function transactionsPath(positionId: string) {
  return `/positions/${encodeURIComponent(positionId)}/transactions`;
}

// In the order the API applies them: oldest first.
export function useTransactions(positionId: string) {
  return useQuery({
    queryKey: transactionsKey(positionId),
    queryFn: () =>
      apiFetch(transactionsPath(positionId), z.array(TransactionSchema)),
  });
}

// A transaction changes the position's quantity, average price and realized
// profit, so the positions and the portfolio are reloaded too.
function useInvalidateAfterChange(positionId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: transactionsKey(positionId) }),
      queryClient.invalidateQueries({ queryKey: positionsKey }),
      queryClient.invalidateQueries({ queryKey: portfolioKey }),
    ]);
}

export function useCreateTransaction(positionId: string) {
  const invalidate = useInvalidateAfterChange(positionId);
  return useMutation({
    mutationFn: (input: CreateTransaction) =>
      apiFetch(transactionsPath(positionId), PositionSchema, {
        method: "POST",
        body: input,
      }),
    onSuccess: invalidate,
  });
}

export function useDeleteTransaction(positionId: string) {
  const invalidate = useInvalidateAfterChange(positionId);
  return useMutation({
    mutationFn: (transaction: Transaction) =>
      apiFetch(`/transactions/${encodeURIComponent(transaction.id)}`, null, {
        method: "DELETE",
      }),
    onSuccess: invalidate,
  });
}
