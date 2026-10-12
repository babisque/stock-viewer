import {
  isClosedPosition,
  portfolioTotals,
  profit,
  realizedProfitTotal,
  type Portfolio,
  type PortfolioPosition,
  type RealizedItem,
  type TotalsItem,
} from "@stock-viewer/shared";
import type { FxService, RateResult } from "../fx/usd-brl";
import type { PrismaClient } from "../lib/prisma";
import type { QuoteService } from "../prices/get-quotes";
import { quoteKey, type Quote } from "../prices/types";
import {
  listLedgerPositions,
  type LedgerPosition,
} from "../positions/positions";

function totalsFor(
  items: TotalsItem[],
  realizedItems: RealizedItem[],
  rate: string,
) {
  const toStrings = (target: "BRL" | "USD") => {
    const totals = portfolioTotals(items, target, rate);
    return {
      invested: totals.invested.toString(),
      currentValue: totals.currentValue.toString(),
      profit: totals.profit.toString(),
      profitPercent: totals.profitPercent.toString(),
      realizedProfit: realizedProfitTotal(
        realizedItems,
        target,
        rate,
      ).toString(),
    };
  };
  return { BRL: toStrings("BRL"), USD: toStrings("USD") };
}

// Pure: combines positions, quotes and the FX rate into the API payload.
// Unpriced and closed positions get null values and are left out of the
// value totals; realized profit counts for every position.
export function buildPortfolio(
  positions: LedgerPosition[],
  quotes: Quote[],
  { fx, stale }: RateResult,
): Portfolio {
  const quotesByKey = new Map(quotes.map((quote) => [quoteKey(quote), quote]));
  const warnings: string[] = [];
  const totalsItems: TotalsItem[] = [];

  const portfolioPositions = positions.map((position): PortfolioPosition => {
    const unvalued = {
      ...position,
      quote: null,
      currentValue: null,
      profit: null,
      profitPercent: null,
    };
    if (isClosedPosition(position)) return unvalued;

    const quote = quotesByKey.get(quoteKey(position));
    if (!quote) {
      warnings.push(
        `${position.ticker} (${position.market}): price unavailable — excluded from totals`,
      );
      return unvalued;
    }

    const result = profit(
      position.quantity,
      position.averagePrice,
      quote.price,
    );
    totalsItems.push({
      currency: position.currency,
      invested: result.invested,
      currentValue: result.currentValue,
    });
    return {
      ...position,
      quote,
      currentValue: result.currentValue.toString(),
      profit: result.amount.toString(),
      profitPercent: result.percent.toString(),
    };
  });

  if (!fx) {
    warnings.push("USD/BRL rate unavailable — totals not shown");
  } else if (stale) {
    warnings.push("USD/BRL rate is outdated — using the last known rate");
  }

  return {
    positions: portfolioPositions,
    totals: fx ? totalsFor(totalsItems, positions, fx.rate) : null,
    fx,
    warnings,
  };
}

export async function getPortfolio({
  prisma,
  quotes,
  fx,
}: {
  prisma: PrismaClient;
  quotes: QuoteService;
  fx: FxService;
}): Promise<Portfolio> {
  const positions = await listLedgerPositions(prisma);
  // Closed positions have nothing to value: don't fetch their quotes.
  const open = positions.filter((position) => !isClosedPosition(position));
  const [priced, rate] = await Promise.all([
    quotes.getQuotes(open),
    fx.getRate(),
  ]);
  return buildPortfolio(positions, priced, rate);
}
