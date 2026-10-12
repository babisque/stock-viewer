import type { Position } from "@stock-viewer/shared";
import { PlusIcon } from "lucide-react";
import { lazy, Suspense, useCallback, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ThemeToggle } from "@/features/preferences/ThemeToggle";
import { usePreferences } from "@/features/preferences/usePreferences";
import { DeletePositionDialog } from "@/features/positions/DeletePositionDialog";
import { TransactionsDialog } from "@/features/transactions/TransactionsDialog";
import { PositionDialog } from "@/features/positions/PositionDialog";
import {
  PositionsTable,
  PositionsTableSkeleton,
} from "@/features/positions/PositionsTable";
import { marketAllocation, positionAllocation } from "./allocation-data";
import { CurrencyToggle } from "./CurrencyToggle";
import { PortfolioWarnings } from "./PortfolioWarnings";
import { RefreshControl, RefreshFailedBanner } from "./RefreshControl";
import { SummaryCards, SummaryCardsSkeleton } from "./SummaryCards";
import { usePortfolio, useRefreshPortfolio } from "./usePortfolio";

// Recharts is large: load it in its own chunk, after the rest of the page.
const AllocationChart = lazy(() =>
  import("./AllocationChart").then((module) => ({
    default: module.AllocationChart,
  })),
);

type DialogState = { open: false } | { open: true; position: Position | null };

function ChartSkeleton() {
  return <Skeleton className="h-56 w-full rounded-lg" />;
}

export function DashboardPage() {
  const { currency, setCurrency, theme, setTheme } = usePreferences();
  const portfolio = usePortfolio();
  const refresh = useRefreshPortfolio();
  const [dialog, setDialog] = useState<DialogState>({ open: false });
  const [deleting, setDeleting] = useState<Position | null>(null);
  // By id, so the dialog always shows the latest portfolio entry.
  const [transactionsOf, setTransactionsOf] = useState<string | null>(null);

  const openAdd = () => setDialog({ open: true, position: null });
  const openEdit = useCallback(
    (position: Position) => setDialog({ open: true, position }),
    [],
  );
  const openTransactions = useCallback(
    (position: Position) => setTransactionsOf(position.id),
    [],
  );

  const data = portfolio.data;
  const transactionsPosition =
    data?.positions.find((position) => position.id === transactionsOf) ?? null;
  const byMarket = useMemo(
    () => (data ? marketAllocation(data, currency) : null),
    [data, currency],
  );
  const byPosition = useMemo(
    () => (data ? positionAllocation(data, currency) : null),
    [data, currency],
  );

  const refreshProps = {
    lastUpdatedAt: portfolio.dataUpdatedAt,
    isRefreshing: refresh.isPending || portfolio.isFetching,
    onRefresh: () => refresh.mutate(),
  };
  const refreshFailed =
    !refresh.isPending &&
    !portfolio.isFetching &&
    (refresh.isError || portfolio.isRefetchError);

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-4 py-6">
      <header className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">stock-viewer</h1>
          <div className="flex flex-wrap items-center gap-2">
            <ThemeToggle value={theme} onChange={setTheme} />
            <Button onClick={openAdd}>
              <PlusIcon /> Add position
            </Button>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <CurrencyToggle
            value={currency}
            onChange={setCurrency}
            disabled={!data?.fx}
          />
          {data && <RefreshControl {...refreshProps} />}
        </div>
      </header>

      {portfolio.isPending ? (
        <>
          <SummaryCardsSkeleton />
          <div className="grid gap-4 lg:grid-cols-2">
            <ChartSkeleton />
            <ChartSkeleton />
          </div>
          <PositionsTableSkeleton />
        </>
      ) : portfolio.isError ? (
        <div role="alert" className="rounded-lg border p-6 text-center">
          <p className="text-destructive mb-3">
            Could not load portfolio: {portfolio.error.message}
          </p>
          <Button variant="outline" onClick={() => portfolio.refetch()}>
            Try again
          </Button>
        </div>
      ) : portfolio.data.positions.length === 0 ? (
        <div className="rounded-lg border border-dashed p-10 text-center">
          <p className="text-muted-foreground mb-4">
            No positions yet — add your first stock
          </p>
          <Button onClick={openAdd}>
            <PlusIcon /> Add position
          </Button>
        </div>
      ) : (
        <>
          {refreshFailed && <RefreshFailedBanner {...refreshProps} />}
          <PortfolioWarnings warnings={portfolio.data.warnings} />
          <SummaryCards
            totals={portfolio.data.totals}
            fx={portfolio.data.fx}
            currency={currency}
          />
          <div className="grid gap-4 lg:grid-cols-2">
            <Suspense
              fallback={
                <>
                  <ChartSkeleton />
                  <ChartSkeleton />
                </>
              }
            >
              {byMarket && (
                <AllocationChart
                  title="By market"
                  slices={byMarket.slices}
                  note={byMarket.note}
                  currency={currency}
                />
              )}
              {byPosition && (
                <AllocationChart
                  title="By position"
                  slices={byPosition.slices}
                  note={byPosition.note}
                  currency={currency}
                />
              )}
            </Suspense>
          </div>
          <PositionsTable
            positions={portfolio.data.positions}
            currency={currency}
            usdBrlRate={portfolio.data.fx?.rate ?? null}
            onTransactions={openTransactions}
            onEdit={openEdit}
            onDelete={setDeleting}
          />
        </>
      )}

      <PositionDialog
        open={dialog.open}
        position={dialog.open ? dialog.position : null}
        onOpenChange={(open) => {
          if (!open) setDialog({ open: false });
        }}
      />
      <DeletePositionDialog
        position={deleting}
        onClose={() => setDeleting(null)}
      />
      <TransactionsDialog
        position={transactionsPosition}
        onClose={() => setTransactionsOf(null)}
      />
    </main>
  );
}
