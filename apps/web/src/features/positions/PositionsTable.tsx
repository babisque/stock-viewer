import {
  compareDecimals,
  formatMoney,
  formatPercent,
  formatQuantity,
  formatSignedMoney,
  investedAmount,
  isClosedPosition,
  moneySortKey,
  type Currency,
  type Market,
  type PortfolioPosition,
} from "@stock-viewer/shared";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
  type Column,
  type ColumnDef,
  type RowData,
  type SortingFn,
} from "@tanstack/react-table";
import {
  ArrowDownIcon,
  ArrowUpDownIcon,
  ArrowUpIcon,
  HistoryIcon,
  PencilIcon,
  Trash2Icon,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Segmented } from "@/components/ui/segmented";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { signClass, sourceAndTime } from "@/lib/display";
import { cn } from "@/lib/utils";

declare module "@tanstack/react-table" {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData extends RowData, TValue> {
    numeric?: boolean; // right-aligned, tabular figures
    className?: string;
  }
}

type PositionActions = {
  onTransactions: (position: PortfolioPosition) => void;
  onEdit: (position: PortfolioPosition) => void;
  onDelete: (position: PortfolioPosition) => void;
};

type PositionsTableProps = PositionActions & {
  positions: PortfolioPosition[];
  currency: Currency; // money columns sort by their value in this currency
  usdBrlRate: string | null;
};

type MarketFilterValue = Market | "all";

// Sort values are decimal strings (or undefined = "can't sort", always last).
const byDecimal: SortingFn<PortfolioPosition> = (a, b, columnId) =>
  compareDecimals(a.getValue<string>(columnId), b.getValue<string>(columnId));

const column = createColumnHelper<PortfolioPosition>();

function Dash() {
  return <span className="text-muted-foreground">—</span>;
}

function Signed({ value, text }: { value: string; text: string }) {
  return <span className={signClass(value)}>{text}</span>;
}

// Column definitions. Cells show each position in its own currency; money
// columns sort by the value converted to `currency`, so R$ and $ amounts are
// never compared as raw numbers.
function positionColumns(
  currency: Currency,
  rate: string | null,
  { onTransactions, onEdit, onDelete }: PositionActions,
): ColumnDef<PortfolioPosition, string | undefined>[] {
  const money = (amount: string | null, position: PortfolioPosition) =>
    moneySortKey(amount, position.currency, currency, rate);
  const numeric = {
    sortingFn: byDecimal,
    sortUndefined: "last",
    sortDescFirst: false,
    meta: { numeric: true },
  } as const;

  return [
    column.accessor("ticker", {
      header: "Ticker",
      sortingFn: "text",
      meta: { className: "font-medium" },
      cell: ({ row: { original: p } }) =>
        isClosedPosition(p) ? (
          <>
            {p.ticker}{" "}
            <span className="text-muted-foreground bg-muted rounded px-1.5 py-0.5 text-xs font-normal">
              closed
            </span>
          </>
        ) : (
          p.ticker
        ),
    }),
    column.accessor("market", {
      header: "Market",
      sortingFn: "text",
      filterFn: "equals",
    }),
    column.accessor("quantity", {
      header: "Quantity",
      ...numeric,
      cell: ({ row }) =>
        formatQuantity(row.original.quantity, row.original.market),
    }),
    column.accessor((p) => money(p.averagePrice, p), {
      id: "averagePrice",
      header: "Avg price",
      ...numeric,
      cell: ({ row }) =>
        formatMoney(row.original.averagePrice, row.original.currency),
    }),
    column.accessor((p) => money(p.quote?.price ?? null, p), {
      id: "currentPrice",
      header: "Current price",
      ...numeric,
      cell: ({ row: { original: p } }) =>
        p.quote ? (
          <>
            <div>{formatMoney(p.quote.price, p.currency)}</div>
            <div className="text-muted-foreground text-xs">
              {sourceAndTime(p.quote.source, p.quote.fetchedAt)}
            </div>
          </>
        ) : (
          <span className="text-muted-foreground italic">
            price unavailable
          </span>
        ),
    }),
    column.accessor(
      (p) => money(investedAmount(p.quantity, p.averagePrice).toString(), p),
      {
        id: "invested",
        header: "Invested",
        ...numeric,
        cell: ({ row: { original: p } }) =>
          formatMoney(investedAmount(p.quantity, p.averagePrice), p.currency),
      },
    ),
    column.accessor((p) => money(p.currentValue, p), {
      id: "currentValue",
      header: "Current value",
      ...numeric,
      cell: ({ row: { original: p } }) =>
        p.currentValue === null ? (
          <Dash />
        ) : (
          formatMoney(p.currentValue, p.currency)
        ),
    }),
    column.accessor((p) => money(p.profit, p), {
      id: "profit",
      header: "Profit",
      ...numeric,
      cell: ({ row: { original: p } }) =>
        p.profit === null ? (
          <Dash />
        ) : (
          <Signed
            value={p.profit}
            text={formatSignedMoney(p.profit, p.currency)}
          />
        ),
    }),
    column.accessor((p) => p.profitPercent ?? undefined, {
      id: "profitPercent",
      header: "Profit %",
      ...numeric,
      cell: ({ row: { original: p } }) =>
        p.profitPercent === null ? (
          <Dash />
        ) : (
          <Signed
            value={p.profitPercent}
            text={formatPercent(p.profitPercent)}
          />
        ),
    }),
    column.accessor((p) => money(p.realizedProfit, p), {
      id: "realizedProfit",
      header: "Realized",
      ...numeric,
      cell: ({ row: { original: p } }) => (
        <Signed
          value={p.realizedProfit}
          text={formatSignedMoney(p.realizedProfit, p.currency)}
        />
      ),
    }),
    column.accessor((p) => p.notes ?? undefined, {
      id: "notes",
      header: "Notes",
      sortingFn: "text",
      sortUndefined: "last",
      meta: { className: "text-muted-foreground max-w-40 truncate" },
    }),
    column.display({
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      meta: { className: "text-right whitespace-nowrap" },
      cell: ({ row: { original: position } }) => {
        const label = `${position.ticker} (${position.market})`;
        return (
          <>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Transactions of ${label}`}
              onClick={() => onTransactions(position)}
            >
              <HistoryIcon />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Edit ${label}`}
              onClick={() => onEdit(position)}
            >
              <PencilIcon />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Delete ${label}`}
              onClick={() => onDelete(position)}
            >
              <Trash2Icon />
            </Button>
          </>
        );
      },
    }),
  ] as ColumnDef<PortfolioPosition, string | undefined>[];
}

function headerClass(meta: { numeric?: boolean } | undefined) {
  return meta?.numeric ? "text-right" : undefined;
}

function cellClass(meta: { numeric?: boolean; className?: string } = {}) {
  return cn(meta.numeric && "text-right tabular-nums", meta.className);
}

function SortIcon({ direction }: { direction: false | "asc" | "desc" }) {
  if (direction === "asc") return <ArrowUpIcon aria-hidden />;
  if (direction === "desc") return <ArrowDownIcon aria-hidden />;
  return <ArrowUpDownIcon aria-hidden className="opacity-40" />;
}

const ARIA_SORT = { asc: "ascending", desc: "descending" } as const;

function SortableHeader({
  column,
  label,
}: {
  column: Column<PortfolioPosition, unknown>;
  label: string;
}) {
  const direction = column.getIsSorted();
  return (
    <button
      type="button"
      onClick={column.getToggleSortingHandler()}
      className={cn(
        "hover:text-foreground inline-flex items-center gap-1 rounded-sm outline-none",
        "focus-visible:ring-ring/50 focus-visible:ring-[3px] [&_svg]:size-3.5",
        column.columnDef.meta?.numeric && "flex-row-reverse",
      )}
    >
      {label}
      <SortIcon direction={direction} />
    </button>
  );
}

export function PositionsTable({
  positions,
  currency,
  usdBrlRate,
  onTransactions,
  onEdit,
  onDelete,
}: PositionsTableProps) {
  const [market, setMarket] = useState<MarketFilterValue>("all");
  const columns = useMemo(
    () =>
      positionColumns(currency, usdBrlRate, {
        onTransactions,
        onEdit,
        onDelete,
      }),
    [currency, usdBrlRate, onTransactions, onEdit, onDelete],
  );
  const columnFilters = useMemo(
    () => (market === "all" ? [] : [{ id: "market", value: market }]),
    [market],
  );

  const table = useReactTable({
    data: positions,
    columns,
    state: { columnFilters },
    getRowId: (position) => position.id,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
  });
  const rows = table.getRowModel().rows;

  return (
    <section aria-label="Positions">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-medium">Positions</h2>
        <Segmented
          label="Filter by market"
          options={[
            { value: "all", label: "All" },
            { value: "B3", label: "B3" },
            { value: "US", label: "US" },
          ]}
          value={market}
          onChange={setMarket}
        />
      </div>
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((group) => (
            <TableRow key={group.id}>
              {group.headers.map((header) => {
                const { meta } = header.column.columnDef;
                const sorted = header.column.getIsSorted();
                const canSort = header.column.getCanSort();
                return (
                  <TableHead
                    key={header.id}
                    className={headerClass(meta)}
                    aria-sort={
                      canSort
                        ? sorted
                          ? ARIA_SORT[sorted]
                          : "none"
                        : undefined
                    }
                  >
                    {canSort ? (
                      <SortableHeader
                        column={header.column}
                        label={String(header.column.columnDef.header)}
                      />
                    ) : (
                      flexRender(
                        header.column.columnDef.header,
                        header.getContext(),
                      )
                    )}
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={columns.length}
                className="text-muted-foreground py-6 text-center"
              >
                No {market} positions.
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => (
              <TableRow
                key={row.id}
                className={cn(
                  isClosedPosition(row.original) && "text-muted-foreground",
                )}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell
                    key={cell.id}
                    className={cellClass(cell.column.columnDef.meta)}
                    title={
                      cell.column.id === "notes"
                        ? (row.original.notes ?? undefined)
                        : undefined
                    }
                  >
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </section>
  );
}

const SKELETON_COLUMNS = positionColumns("BRL", null, {
  onTransactions: () => {},
  onEdit: () => {},
  onDelete: () => {},
});

export function PositionsTableSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <Table aria-busy="true" aria-label="Loading portfolio">
      <TableHeader>
        <TableRow>
          {SKELETON_COLUMNS.map((def, index) => (
            <TableHead key={index} className={headerClass(def.meta)}>
              {typeof def.header === "string" ? def.header : null}
            </TableHead>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {Array.from({ length: rows }, (_, row) => (
          <TableRow key={row}>
            {SKELETON_COLUMNS.map((_, cell) => (
              <TableCell key={cell}>
                <Skeleton className="h-5 w-full" />
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
