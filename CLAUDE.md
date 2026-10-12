# stock-viewer
Personal portfolio tracker for Brazilian (B3) and US stocks. Positions (ticker, quantity, average price) are entered manually; current prices come from free sources and the app calculates profit/loss.

## Specs
- Development happens in phases; see `specs/README.md` for the roadmap and status.
- Before working on a feature, read the spec of the current phase and follow its boundaries.
- Don't implement anything from a later phase without asking.

## Stack
- Node 26 + TypeScript (strict mode) everywhere
- npm workspaces monorepo:
  - `apps/api` — Fastify REST API
  - `apps/web` — React + Vite SPA
  - `packages/shared` — shared types and pure calculation functions
- SQLite via Prisma
- Vitest for tests

## Price sources
- Primary: Yahoo Finance via `yahoo-finance2` (B3 tickers use the `.SA` suffix, e.g. `PETR4.SA`)
- Fallback for B3: brapi.dev
- USD/BRL rate: AwesomeAPI (`economia.awesomeapi.com.br`)
- All providers sit behind one `PriceProvider` interface in `apps/api/src/prices/`; cache quotes (quotes are ~15 min delayed anyway, so don't hammer the sources).

## Run commands
- dev (api + web): `npm run dev`
- test: `npm test`
- typecheck: `npm run typecheck` (`tsc --noEmit` in each workspace)
- db migration: `npx prisma migrate dev` (in `apps/api`)

## Conventions
- Prefer small, pure functions over classes.
- No complex abstractions — prioritize clarity over brevity.
- Every new feature requires a test file.
- Money: never use plain floats for stored values or totals; use Prisma `Decimal` / a decimal library and round only for display.
- Every monetary value carries its currency (`BRL` | `USD`); never add values in different currencies without an explicit conversion.
- Profit calculations live in `packages/shared` as pure functions with tests.

## Off-limits (never touch without explicit instruction)
- Any database schema migration — propose a plan first.
- `.env` files and API tokens.

## Fallbacks (when in doubt)
- Propose 2 options and ask me to choose.
- Never rename public APIs (REST routes, shared types) without asking.
