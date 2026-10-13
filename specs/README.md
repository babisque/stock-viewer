# stock-viewer — Roadmap

Each phase has its own spec. A phase is only started after the previous one meets its success criteria.

The app runs on .NET 10 + Aspire + minimal APIs + Blazor Web App. Phases 1–4 were first built in Node/TypeScript and then ported to .NET with the same behaviour and REST contract (M1–M4, see [migration/README.md](migration/README.md)). The Phase 1–4 specs are kept as history: their behaviour is what the .NET app implements, but their stack and file references are the old Node ones.

| Phase | Spec | Goal | Status |
|---|---|---|---|
| 1 | [phase-1-foundation.md](phase-1-foundation.md) | Setup + manual position management (CRUD) | Done (ported to .NET) |
| 2 | [phase-2-prices-and-profit.md](phase-2-prices-and-profit.md) | Live quotes, USD/BRL rate and profit calculation | Done (ported to .NET) |
| 3 | [phase-3-dashboard.md](phase-3-dashboard.md) | Portfolio dashboard: totals, allocation chart, auto-refresh | Done (ported to .NET) |
| 4 | [phase-4-transactions.md](phase-4-transactions.md) | Buy/sell history, automatic average price, realized profit | Done (ported to .NET) |

## Migration to .NET
| Phase | Spec | Status |
|---|---|---|
| M1–M4 | [migration/README.md](migration/README.md) | Done (M3 pending manual UI check) |

New feature phases (5+) get new specs written for the .NET stack.

## Future ideas (no spec yet)
- Dividends / JCP tracking
- Brazilian income tax (IR) helper: monthly sales, R$20k exemption, DARF
- Historical portfolio value chart
- Price alerts

## Shared definitions
- **Market:** `B3` (Brazil, currency `BRL`) or `US` (currency `USD`). The currency is always derived from the market.
- **Ticker:** stored without suffix (`PETR4`, `AAPL`). Provider-specific symbols (`PETR4.SA`) are built only inside the price providers.
- **Money:** C# `decimal` in code (never `double`/`float`), decimal **strings** in JSON (`"28.45"`). Round only when formatting for display.
