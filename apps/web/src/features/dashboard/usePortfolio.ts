import { PortfolioSchema } from "@stock-viewer/shared";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";

export const portfolioKey = ["portfolio"] as const;

// Quotes are ~15 min delayed and cached server-side for 5 min, so there is no
// point in asking more often. Nothing may refresh more often than every 60 s.
export const AUTO_REFRESH_MS = 5 * 60 * 1000;
export const MIN_REFRESH_MS = 60 * 1000;

export function usePortfolio() {
  return useQuery({
    queryKey: portfolioKey,
    queryFn: () => apiFetch("/portfolio", PortfolioSchema),
    refetchInterval: AUTO_REFRESH_MS,
    refetchIntervalInBackground: false, // only while the tab is visible
    staleTime: MIN_REFRESH_MS, // no refetch on window focus within 60 s
  });
}

// Clears the server-side cache and fetches fresh quotes. The result replaces
// the cached portfolio, which also moves its `dataUpdatedAt` forward.
export function useRefreshPortfolio() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiFetch("/portfolio/refresh", PortfolioSchema, { method: "POST" }),
    onSuccess: (portfolio) => {
      queryClient.setQueryData(portfolioKey, portfolio);
    },
  });
}

// Whole seconds left before the next manual refresh is allowed (0 = now).
export function cooldownRemaining(now: number, lastUpdatedAt: number): number {
  const remainingMs = lastUpdatedAt + MIN_REFRESH_MS - now;
  return remainingMs > 0 ? Math.ceil(remainingMs / 1000) : 0;
}
