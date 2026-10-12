import {
  formatMoney,
  formatPercent,
  formatSignedMoney,
  type Currency,
  type Portfolio,
} from "@stock-viewer/shared";
import type { ReactNode } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import { signClass, sourceAndTime } from "@/lib/display";
import { cn } from "@/lib/utils";

type SummaryCardsProps = Pick<Portfolio, "totals" | "fx"> & {
  currency: Currency;
};

const CARD_CLASS = "bg-card text-card-foreground rounded-lg border p-4";
const GRID_CLASS = "grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5";

function Card({
  title,
  children,
  detail,
}: {
  title: string;
  children: ReactNode;
  detail?: ReactNode;
}) {
  return (
    <div className={CARD_CLASS}>
      <dt className="text-muted-foreground text-sm">{title}</dt>
      <dd className="mt-1 text-2xl font-semibold tabular-nums">{children}</dd>
      {detail && (
        <dd className="text-muted-foreground mt-1 text-xs">{detail}</dd>
      )}
    </div>
  );
}

export function SummaryCards({ totals, fx, currency }: SummaryCardsProps) {
  const row = totals?.[currency];
  return (
    <section aria-label="Portfolio summary">
      <dl className={GRID_CLASS}>
        <Card title="Total invested">
          {row ? formatMoney(row.invested, currency) : "—"}
        </Card>
        <Card title="Current value">
          {row ? formatMoney(row.currentValue, currency) : "—"}
        </Card>
        <Card
          title="Unrealized profit"
          detail={
            row && (
              <span className={cn("text-sm", signClass(row.profitPercent))}>
                {formatPercent(row.profitPercent)}
              </span>
            )
          }
        >
          {row ? (
            <span className={signClass(row.profit)}>
              {formatSignedMoney(row.profit, currency)}
            </span>
          ) : (
            "—"
          )}
        </Card>
        <Card title="Realized profit">
          {row ? (
            <span className={signClass(row.realizedProfit)}>
              {formatSignedMoney(row.realizedProfit, currency)}
            </span>
          ) : (
            "—"
          )}
        </Card>
        <Card
          title="USD/BRL"
          detail={fx ? sourceAndTime(fx.source, fx.fetchedAt) : "unavailable"}
        >
          {fx ? formatMoney(fx.rate, "BRL") : "—"}
        </Card>
      </dl>

      <p className="text-muted-foreground mt-2 text-xs">
        {totals
          ? "Invested, value and unrealized profit exclude positions without a price; realized profit comes from your sells. Totals convert at the current USD/BRL rate. Prices may be delayed ~15 min."
          : "Totals are unavailable without the USD/BRL rate."}
      </p>
    </section>
  );
}

export function SummaryCardsSkeleton() {
  return (
    <div className={GRID_CLASS} aria-hidden>
      {Array.from({ length: 5 }, (_, index) => (
        <div key={index} className={CARD_CLASS}>
          <Skeleton className="h-4 w-24" />
          <Skeleton className="mt-2 h-8 w-32" />
        </div>
      ))}
    </div>
  );
}
