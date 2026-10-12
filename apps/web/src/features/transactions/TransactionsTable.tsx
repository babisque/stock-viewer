import {
  formatMoney,
  formatQuantity,
  type Currency,
  type Market,
  type Transaction,
} from "@stock-viewer/shared";
import { Trash2Icon } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatTradeDate } from "@/lib/display";
import { cn } from "@/lib/utils";
import { transactionTotal } from "./transactionForm";

type TransactionsTableProps = {
  transactions: Transaction[]; // oldest first, as the API returns them
  market: Market;
  currency: Currency;
  onDelete: (transaction: Transaction) => Promise<unknown>;
  isDeleting: boolean;
};

const TYPE_LABELS = { BUY: "Buy", SELL: "Sell" } as const;
const NUMERIC = "text-right tabular-nums";

export function TransactionsTable({
  transactions,
  market,
  currency,
  onDelete,
  isDeleting,
}: TransactionsTableProps) {
  // Deleting asks for a confirmation inside the row.
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const newestFirst = [...transactions].reverse();

  async function confirmDelete(transaction: Transaction) {
    try {
      await onDelete(transaction);
    } finally {
      setConfirmingId(null);
    }
  }

  return (
    <Table aria-label="Transactions">
      <TableHeader>
        <TableRow>
          <TableHead>Date</TableHead>
          <TableHead>Type</TableHead>
          <TableHead className="text-right">Quantity</TableHead>
          <TableHead className="text-right">Unit price</TableHead>
          <TableHead className="text-right">Fees</TableHead>
          <TableHead className="text-right">Total</TableHead>
          <TableHead>
            <span className="sr-only">Actions</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {newestFirst.length === 0 ? (
          <TableRow>
            <TableCell
              colSpan={7}
              className="text-muted-foreground py-6 text-center"
            >
              No transactions.
            </TableCell>
          </TableRow>
        ) : (
          newestFirst.map((transaction) => {
            const label = `${TYPE_LABELS[transaction.type]} of ${formatTradeDate(transaction.date)}`;
            const total = transactionTotal(transaction);
            return (
              <TableRow key={transaction.id}>
                <TableCell>{formatTradeDate(transaction.date)}</TableCell>
                <TableCell
                  className={cn(
                    "font-medium",
                    transaction.type === "SELL" && "text-muted-foreground",
                  )}
                >
                  {TYPE_LABELS[transaction.type]}
                </TableCell>
                <TableCell className={NUMERIC}>
                  {formatQuantity(transaction.quantity, market)}
                </TableCell>
                <TableCell className={NUMERIC}>
                  {formatMoney(transaction.unitPrice, currency)}
                </TableCell>
                <TableCell className={NUMERIC}>
                  {formatMoney(transaction.fees, currency)}
                </TableCell>
                <TableCell className={NUMERIC}>
                  {total ? formatMoney(total, currency) : "—"}
                </TableCell>
                <TableCell className="text-right whitespace-nowrap">
                  {confirmingId === transaction.id ? (
                    <span className="inline-flex items-center gap-1">
                      <span className="text-muted-foreground text-xs">
                        Delete?
                      </span>
                      <Button
                        size="sm"
                        variant="destructive"
                        disabled={isDeleting}
                        onClick={() => confirmDelete(transaction)}
                      >
                        {isDeleting ? "Deleting…" : "Delete"}
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={isDeleting}
                        onClick={() => setConfirmingId(null)}
                      >
                        Keep
                      </Button>
                    </span>
                  ) : (
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Delete ${label}`}
                      onClick={() => setConfirmingId(transaction.id)}
                    >
                      <Trash2Icon />
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            );
          })
        )}
      </TableBody>
    </Table>
  );
}
