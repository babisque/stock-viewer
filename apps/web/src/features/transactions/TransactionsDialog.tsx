import {
  formatMoney,
  formatQuantity,
  formatSignedMoney,
  isClosedPosition,
  type PortfolioPosition,
} from "@stock-viewer/shared";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { signClass } from "@/lib/display";
import { cn } from "@/lib/utils";
import { TransactionForm } from "./TransactionForm";
import { TransactionsTable } from "./TransactionsTable";
import {
  useCreateTransaction,
  useDeleteTransaction,
  useTransactions,
} from "./useTransactions";

type TransactionsDialogProps = {
  // null = closed. Pass the latest portfolio entry so the summary updates
  // after every change.
  position: PortfolioPosition | null;
  onClose: () => void;
};

export function TransactionsDialog({
  position,
  onClose,
}: TransactionsDialogProps) {
  return (
    <Dialog
      open={position !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent className="sm:max-w-3xl">
        {position && <TransactionsPanel position={position} />}
      </DialogContent>
    </Dialog>
  );
}

function TransactionsPanel({ position }: { position: PortfolioPosition }) {
  const transactions = useTransactions(position.id);
  const createTransaction = useCreateTransaction(position.id);
  const deleteTransaction = useDeleteTransaction(position.id);
  const { currency, market } = position;

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          {position.ticker} ({position.market}) transactions
        </DialogTitle>
        <DialogDescription>
          Quantity, average price and realized profit are calculated from these
          buys and sells. Prices and fees are in {currency}.
        </DialogDescription>
      </DialogHeader>

      <dl className="grid grid-cols-3 gap-3 rounded-lg border p-3 text-sm">
        <div>
          <dt className="text-muted-foreground">Quantity</dt>
          <dd className="font-medium tabular-nums">
            {formatQuantity(position.quantity, market)}
            {isClosedPosition(position) && (
              <span className="text-muted-foreground ml-1 font-normal">
                (closed)
              </span>
            )}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Avg price</dt>
          <dd className="font-medium tabular-nums">
            {formatMoney(position.averagePrice, currency)}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Realized profit</dt>
          <dd
            className={cn(
              "font-medium tabular-nums",
              signClass(position.realizedProfit),
            )}
          >
            {formatSignedMoney(position.realizedProfit, currency)}
          </dd>
        </div>
      </dl>

      <TransactionForm
        market={market}
        currency={currency}
        available={position.quantity}
        onSubmit={(input) => createTransaction.mutateAsync(input)}
      />

      {deleteTransaction.error && (
        <p role="alert" className="text-destructive text-sm">
          {deleteTransaction.error.message}
        </p>
      )}

      <div className="max-h-72 overflow-y-auto">
        {transactions.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : transactions.isError ? (
          <p role="alert" className="text-destructive text-sm">
            Could not load transactions: {transactions.error.message}
          </p>
        ) : (
          <TransactionsTable
            transactions={transactions.data}
            market={market}
            currency={currency}
            isDeleting={deleteTransaction.isPending}
            onDelete={async (transaction) => {
              deleteTransaction.reset();
              await deleteTransaction.mutateAsync(transaction).catch(() => {
                // Shown above the table from the mutation state.
              });
            }}
          />
        )}
      </div>
    </>
  );
}
