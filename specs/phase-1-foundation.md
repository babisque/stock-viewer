# Phase 1 Spec: Foundation & Position Management

## 1. Overview and Goal
* **Purpose:** Set up the TypeScript monorepo and let the user manually register, edit and delete stock positions (ticker, market, quantity, average price). No live prices yet.
* **Success Criteria:**
  * `npm run dev` starts the API and the web app with one command.
  * The user can add a position (e.g. `PETR4`, B3, 100 shares, R$ 28.45) and see it in a table after reloading the page.
  * The user can edit and delete positions; invalid input (negative quantity, empty ticker, duplicate ticker+market) shows a clear error.
  * Invested amount (quantity × average price) is shown per position, in the position's currency.
  * `npm test` and `npm run typecheck` pass with zero errors.

## 2. Tech Stack and Architecture
*(Pin exact versions in `package.json` at setup time, using the latest stable release of each major below.)*
* **Runtime:** Node 26 (`engines: ">=26"`), TypeScript 5.x with `strict: true`, `noUncheckedIndexedAccess: true`.
* **Monorepo:** npm workspaces.
* **Backend:** Fastify 5 + `fastify-type-provider-zod`.
* **Database:** SQLite via Prisma ORM. DB file at `apps/api/prisma/dev.db` (git-ignored).
* **Validation:** Zod 4 schemas in `packages/shared`, used by both API and web (types via `z.infer`).
* **Decimals:** `decimal.js`.
* **Frontend:** React 19 + Vite + TypeScript.
* **Styling/UI:** Tailwind CSS 4 + shadcn/ui.
* **Data fetching:** TanStack Query 5.
* **Forms:** React Hook Form + `@hookform/resolvers/zod`.
* **Tests:** Vitest (all workspaces), `@testing-library/react` for components.

### Data model (Prisma)
```prisma
enum Market {
  B3
  US
}

model Position {
  id           String   @id @default(cuid())
  ticker       String            // uppercase, no suffix: "PETR4", "AAPL"
  market       Market
  quantity     Decimal           // Decimal allows fractional US shares
  averagePrice Decimal           // in the market's currency
  notes        String?
  createdAt    DateTime @default(now())
  updatedAt    DateTime @updatedAt

  @@unique([ticker, market])
}
```

### API contract
Base path: `/api`. Decimals are strings in JSON.

| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/positions` | — | `Position[]` |
| POST | `/positions` | `CreatePosition` | `201` + `Position` |
| PUT | `/positions/:id` | `UpdatePosition` | `Position` |
| DELETE | `/positions/:id` | — | `204` |
| GET | `/health` | — | `{ status: "ok" }` |

Errors use a single shape: `{ error: { code: string, message: string } }` (`400` validation, `404` not found, `409` duplicate).

## 3. Executable Commands
* **Install:** `npm install`
* **Dev (api + web):** `npm run dev` (runs both workspaces in parallel)
* **Typecheck:** `npm run typecheck` → `tsc --noEmit -p <workspace>` for each workspace
* **Tests:** `npm test` → `vitest run`
* **Tests (watch):** `npm run test:watch`
* **Lint/format:** `npm run lint` (ESLint) / `npm run format` (Prettier)
* **Migration:** `npm run db:migrate -w apps/api` → `prisma migrate dev`
* **DB GUI:** `npm run db:studio -w apps/api` → `prisma studio`

## 4. Project Structure
```
stock-viewer/
├── apps/
│   ├── api/
│   │   ├── prisma/schema.prisma
│   │   └── src/
│   │       ├── server.ts           # Fastify bootstrap only
│   │       ├── routes/             # thin HTTP handlers, one file per resource
│   │       ├── positions/          # domain logic + DB access for positions
│   │       └── lib/                # prisma client, error helpers
│   └── web/
│       └── src/
│           ├── features/positions/ # hooks, forms, tables for positions
│           ├── components/ui/      # shadcn/ui pure presentational components
│           └── lib/                # api client, formatters
├── packages/
│   └── shared/src/
│       ├── schemas/                # Zod schemas (source of truth for types)
│       └── money/                  # pure decimal/currency helpers
└── specs/
```
* Route handlers only parse input, call domain functions and map errors — no business logic.
* Tests sit next to the code: `foo.ts` → `foo.test.ts`.

## 5. Code Style and Conventions
* **Typing:** strict TypeScript, no `any`. Types come from Zod via `z.infer`, never written twice.
* **Functions over classes:** small, pure functions; side effects (DB, HTTP) at the edges.
* **Money:** `decimal.js` for all math; round only when formatting for display.
* **Formatting:** `Intl.NumberFormat` — `pt-BR`/`BRL` for B3, `en-US`/`USD` for US.
* **Tickers:** normalize with `trim().toUpperCase()` at the schema level.

Example of the expected style:
```ts
// packages/shared/src/money/invested.ts
import Decimal from "decimal.js";

export function investedAmount(quantity: string, averagePrice: string): Decimal {
  return new Decimal(quantity).times(averagePrice);
}
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Validate every request body with the shared Zod schema.
  * Write a test file for each new function, route and form.
  * Show loading skeletons and empty states ("No positions yet — add your first stock").
* **Ask first:**
  * Before changing the Prisma schema after the first migration.
  * Before adding dependencies not listed in this spec.
  * Before changing the API contract above.
* **Never do:**
  * Never use `number`/`parseFloat` for money.
  * Never commit `dev.db` or `.env` files.
  * Never fetch external price data in this phase.
