import {
  PositionSchema,
  type CreatePosition,
  type Position,
  type UpdatePosition,
} from "@stock-viewer/shared";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { portfolioKey } from "@/features/dashboard/usePortfolio";
import { apiFetch } from "@/lib/api";

export const positionsKey = ["positions"] as const;

export function usePositions() {
  return useQuery({
    queryKey: positionsKey,
    queryFn: () => apiFetch("/positions", z.array(PositionSchema)),
  });
}

// The portfolio is built from the positions, so refresh both after a change.
function useInvalidatePositions() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: positionsKey }),
      queryClient.invalidateQueries({ queryKey: portfolioKey }),
    ]);
}

export function useCreatePosition() {
  const invalidate = useInvalidatePositions();
  return useMutation({
    mutationFn: (input: CreatePosition) =>
      apiFetch("/positions", PositionSchema, { method: "POST", body: input }),
    onSuccess: invalidate,
  });
}

export function useUpdatePosition() {
  const invalidate = useInvalidatePositions();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdatePosition }) =>
      apiFetch(`/positions/${encodeURIComponent(id)}`, PositionSchema, {
        method: "PUT",
        body: input,
      }),
    onSuccess: invalidate,
  });
}

export function useDeletePosition() {
  const invalidate = useInvalidatePositions();
  return useMutation({
    mutationFn: (position: Position) =>
      apiFetch(`/positions/${encodeURIComponent(position.id)}`, null, {
        method: "DELETE",
      }),
    onSuccess: invalidate,
  });
}
