# stock-viewer — Roadmap

Each phase has its own spec. A phase is only started after the previous one meets its success criteria.

| Phase | Spec | Goal | Status |
|---|---|---|---|
| 1 | [phase-1-foundation.md](phase-1-foundation.md) | Monorepo setup + manual position management (CRUD) | Done (pending manual UI check) |
| 2 | [phase-2-prices-and-profit.md](phase-2-prices-and-profit.md) | Live quotes, USD/BRL rate and profit calculation | Done (pending manual UI check) |
| 3 | [phase-3-dashboard.md](phase-3-dashboard.md) | Portfolio dashboard: totals, allocation chart, auto-refresh | Done (pending manual UI check) |
| 4 | [phase-4-transactions.md](phase-4-transactions.md) | Buy/sell history, automatic average price, realized profit | Done (pending manual UI check) |

## Future ideas (no spec yet)
- Dividends / JCP tracking
- Brazilian income tax (IR) helper: monthly sales, R$20k exemption, DARF
- Historical portfolio value chart
- Price alerts

## Shared definitions
- **Market:** `B3` (Brazil, currency `BRL`) or `US` (currency `USD`). The currency is always derived from the market.
- **Ticker:** stored without suffix (`PETR4`, `AAPL`). Provider-specific symbols (`PETR4.SA`) are built only inside the price providers.
- **Money:** decimals serialized as **strings** in JSON (`"28.45"`), handled with `decimal.js` in code. Never plain `number` for money.
