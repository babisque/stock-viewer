# stock-viewer
Personal portfolio tracker for Brazilian (B3) and US stocks. Positions (ticker, quantity, average price) are entered manually; current prices come from free sources and the app calculates profit/loss.

## Specs
- Development happens in phases; see `specs/README.md` for the roadmap and status.
- Before working on a feature, read the spec of the current phase and follow its boundaries.
- Don't implement anything from a later phase without asking.

## Stack
- .NET 10, C# with nullable enabled and warnings as errors everywhere
- Aspire orchestrates the app (`api` + `web`)
- Projects:
  - `src/StockViewer.AppHost` — Aspire app model
  - `src/StockViewer.ServiceDefaults` — OpenTelemetry, health checks, service discovery, HttpClient resilience
  - `src/StockViewer.Shared` — shared contracts, validation and pure calculation functions (BCL only)
  - `src/StockViewer.Api` — minimal API REST backend
  - `src/StockViewer.Web` — Blazor Web App (Interactive Server)
  - `tests/StockViewer.{Shared,Api,Web}.Tests`
- SQLite via EF Core
- xUnit v3 + bUnit for tests

## Price sources
- Primary: Yahoo Finance via its public HTTP endpoints (B3 tickers use the `.SA` suffix, e.g. `PETR4.SA`)
- Fallback for B3: brapi.dev
- USD/BRL rate: AwesomeAPI (`economia.awesomeapi.com.br`)
- All providers sit behind one `IPriceProvider` interface in `src/StockViewer.Api/Prices/`; cache quotes (quotes are ~15 min delayed anyway, so don't hammer the sources).

## Run commands
- dev (api + web): `aspire run`
- test: `dotnet test`
- build: `dotnet build -warnaserror`
- db migration: `dotnet ef migrations add <Name> --project src/StockViewer.Api`

## Conventions
- Prefer small, pure (static) functions over classes.
- No complex abstractions — prioritize clarity over brevity.
- Every new feature requires a test file.
- Money: only `decimal`, never `double`/`float`; round only for display.
- Every monetary value carries its currency (`BRL` | `USD`, via `Money`); never add values in different currencies without an explicit conversion.
- Profit calculations live in `StockViewer.Shared` as pure functions with tests.

## Off-limits (never touch without explicit instruction)
- Any database schema migration — propose a plan first.
- `.env` files, user-secrets and API tokens.

## Fallbacks (when in doubt)
- Propose 2 options and ask me to choose.
- Never rename public APIs (REST routes, shared types) without asking.
