import { RefreshCwIcon, TriangleAlertIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { formatTime } from "@/lib/display";
import { cn } from "@/lib/utils";
import { cooldownRemaining } from "./usePortfolio";

type RefreshControlProps = {
  lastUpdatedAt: number; // when the portfolio was last loaded (ms)
  isRefreshing: boolean;
  onRefresh: () => void;
};

// Re-renders every second while `active`, so a countdown stays current.
function useTicker(active: boolean) {
  const [, setTick] = useState(0);
  useEffect(() => {
    if (!active) return;
    const id = setInterval(() => setTick((tick) => tick + 1), 1000);
    return () => clearInterval(id);
  }, [active]);
}

function useCooldown(lastUpdatedAt: number) {
  const remaining = cooldownRemaining(Date.now(), lastUpdatedAt);
  useTicker(remaining > 0);
  return remaining;
}

export function RefreshControl({
  lastUpdatedAt,
  isRefreshing,
  onRefresh,
}: RefreshControlProps) {
  const remaining = useCooldown(lastUpdatedAt);
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
      <Button
        variant="outline"
        size="sm"
        onClick={onRefresh}
        disabled={isRefreshing || remaining > 0}
      >
        <RefreshCwIcon className={cn(isRefreshing && "animate-spin")} />
        {isRefreshing
          ? "Refreshing…"
          : remaining > 0
            ? `Refresh in ${remaining}s`
            : "Refresh"}
      </Button>
      <span className="text-muted-foreground text-xs">
        Last updated at {formatTime(lastUpdatedAt)}
      </span>
    </div>
  );
}

// Shown when a refresh fails but older data is still on screen.
export function RefreshFailedBanner({
  lastUpdatedAt,
  isRefreshing,
  onRefresh,
}: RefreshControlProps) {
  const remaining = useCooldown(lastUpdatedAt);
  return (
    <div
      role="alert"
      className="border-destructive/40 text-destructive flex flex-wrap items-center gap-3 rounded-lg border p-3 text-sm"
    >
      <TriangleAlertIcon className="size-4 shrink-0" aria-hidden />
      <span className="flex-1">
        Couldn&apos;t refresh — showing data from {formatTime(lastUpdatedAt)}.
      </span>
      <Button
        variant="outline"
        size="sm"
        onClick={onRefresh}
        disabled={isRefreshing || remaining > 0}
      >
        {remaining > 0 ? `Try again in ${remaining}s` : "Try again"}
      </Button>
    </div>
  );
}
