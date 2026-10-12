import type {
  Portfolio,
  PortfolioPosition,
  Position,
  Transaction,
} from "@stock-viewer/shared";

export function makePosition(overrides: Partial<Position> = {}): Position {
  return {
    id: "pos-1",
    ticker: "PETR4",
    market: "B3",
    currency: "BRL",
    quantity: "100",
    averagePrice: "28.45",
    notes: null,
    createdAt: "2026-10-11T00:00:00.000Z",
    updatedAt: "2026-10-11T00:00:00.000Z",
    ...overrides,
  };
}

// Intl uses non-breaking spaces in pt-BR currency output.
export function normalizeSpaces(value: string | null | undefined): string {
  return (value ?? "").replace(/\s/g, " ");
}

export const QUOTE_TIME = "2026-10-12T14:32:00.000Z";

// PETR4: 100 @ 28.45, now 30 (Yahoo).
export function makePortfolioPosition(
  overrides: Partial<PortfolioPosition> = {},
): PortfolioPosition {
  return {
    ...makePosition(),
    quote: {
      ticker: "PETR4",
      market: "B3",
      price: "30",
      currency: "BRL",
      source: "yahoo",
      fetchedAt: QUOTE_TIME,
    },
    currentValue: "3000",
    profit: "155",
    profitPercent: "5.448154657293497364",
    realizedProfit: "0",
    ...overrides,
  };
}

// AAPL: 2 @ $100, now $90 (a loss).
export function makeUsPortfolioPosition(
  overrides: Partial<PortfolioPosition> = {},
): PortfolioPosition {
  return makePortfolioPosition({
    ...makePosition({
      id: "pos-2",
      ticker: "AAPL",
      market: "US",
      currency: "USD",
      quantity: "2",
      averagePrice: "100",
    }),
    quote: {
      ticker: "AAPL",
      market: "US",
      price: "90",
      currency: "USD",
      source: "yahoo",
      fetchedAt: QUOTE_TIME,
    },
    currentValue: "180",
    profit: "-20",
    profitPercent: "-10",
    ...overrides,
  });
}

// A B3 position no provider could price.
export function makeUnpricedPosition(
  overrides: Partial<PortfolioPosition> = {},
): PortfolioPosition {
  return makePortfolioPosition({
    ...makePosition({ id: "pos-3", ticker: "XXXX3" }),
    quote: null,
    currentValue: null,
    profit: null,
    profitPercent: null,
    ...overrides,
  });
}

export function makePortfolio(overrides: Partial<Portfolio> = {}): Portfolio {
  return {
    positions: [makePortfolioPosition()],
    totals: {
      BRL: {
        invested: "2845",
        currentValue: "3000",
        profit: "155",
        profitPercent: "5.448154657293497364",
        realizedProfit: "0",
      },
      USD: {
        invested: "569",
        currentValue: "600",
        profit: "31",
        profitPercent: "5.448154657293497364",
        realizedProfit: "0",
      },
    },
    fx: { rate: "5", source: "awesomeapi", fetchedAt: QUOTE_TIME },
    warnings: [],
    ...overrides,
  };
}

// PETR4 fully sold: quantity 0, R$ 300 realized, nothing to value.
export function makeClosedPosition(
  overrides: Partial<PortfolioPosition> = {},
): PortfolioPosition {
  return makePortfolioPosition({
    ...makePosition({ id: "pos-4", ticker: "BBAS3", quantity: "0" }),
    quote: null,
    currentValue: null,
    profit: null,
    profitPercent: null,
    realizedProfit: "300",
    ...overrides,
  });
}

export function makeTransaction(
  overrides: Partial<Transaction> = {},
): Transaction {
  return {
    id: "tx-1",
    positionId: "pos-1",
    type: "BUY",
    date: "2026-10-01",
    quantity: "100",
    unitPrice: "28.45",
    fees: "0",
    currency: "BRL",
    createdAt: "2026-10-01T12:00:00.000Z",
    ...overrides,
  };
}
