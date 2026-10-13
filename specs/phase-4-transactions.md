# Phase 4 Spec: Transactions & Automatic Average Price

> **Implemented in Node; ported to .NET in [`specs/migration/`](migration/README.md).** The Node/TypeScript code was removed in M4. Kept as history: the behaviour described here is what the .NET app implements, but stack and file references are the old Node ones.

## 1. Overview and Goal
* **Purpose:** Instead of typing the average price by hand, the user records each buy and sell. The app computes quantity, average price and realized profit from the transaction history.
* **Success Criteria:**
  * The user can add a BUY or SELL with date, quantity, unit price and fees.
  * Quantity and average price for each position are derived from its transactions.
  * Selling does not change the average price; it creates realized profit (`(sellPrice − averagePrice) × qty − fees`).
  * Realized profit is shown per position and in the totals, separate from unrealized profit.
  * Existing Phase 1 positions are migrated as a single initial BUY transaction — no data is lost.
  * A sell larger than the current quantity is rejected.

## 2. Tech Stack and Architecture
Same stack as previous phases. No new dependencies expected.

### Data model (proposal — requires approval before migrating)
```prisma
enum TransactionType {
  BUY
  SELL
}

model Transaction {
  id         String          @id @default(cuid())
  positionId String
  position   Position        @relation(fields: [positionId], references: [id], onDelete: Cascade)
  type       TransactionType
  date       DateTime
  quantity   Decimal
  unitPrice  Decimal
  fees       Decimal         @default(0)
  createdAt  DateTime        @default(now())

  @@index([positionId, date])
}
```
* `Position.quantity` and `Position.averagePrice` become derived values (computed, not edited directly).
* **Migration plan:** (1) add `Transaction` table; (2) for each existing position create a BUY with `quantity`, `unitPrice = averagePrice`, `date = createdAt`; (3) verify totals match before/after; (4) only then stop accepting manual edits of quantity/average price.

### Average price rules (Brazilian standard, `packages/shared/src/money/average-price.ts`)
* BUY: `newAvg = (qty × avg + buyQty × buyPrice + fees) ÷ (qty + buyQty)`
* SELL: average price unchanged; `qty -= sellQty`.
* Position fully sold (qty = 0): average resets to 0 on next BUY.
* Transactions are processed in date order (ties by `createdAt`).

### API contract (additions)
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/positions/:id/transactions` | — | `Transaction[]` |
| POST | `/positions/:id/transactions` | `CreateTransaction` | `201` + updated `Position` |
| DELETE | `/transactions/:id` | — | `204` |

`PortfolioPosition` and `Totals` gain `realizedProfit`.

## 3. Executable Commands
Same as Phase 1.
* **Migration:** `npm run db:migrate -w apps/api` (only after the plan above is approved)
* **Back up DB first:** `cp apps/api/prisma/dev.db apps/api/prisma/dev.db.bak`

## 4. Project Structure
```
apps/api/src/transactions/
packages/shared/src/money/average-price.ts
apps/web/src/features/transactions/
```

## 5. Code Style and Conventions
* Same as previous phases.
* Derivation is a pure fold over sorted transactions — easy to test with tables of cases:

```ts
export function derivePosition(txs: Transaction[]): { quantity: Decimal; averagePrice: Decimal; realizedProfit: Decimal } {
  return sortByDate(txs).reduce(applyTransaction, emptyPosition());
}
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Back up `dev.db` before running the migration.
  * Test the average-price function with: multiple buys, partial sell, full sell then rebuy, fees.
* **Ask first:**
  * Before running the migration (present the plan and the before/after check).
  * Before handling splits, bonuses (bonificação) or ticker changes — out of scope for now.
* **Never do:**
  * Never delete the original Phase 1 data during migration.
  * Never allow editing derived quantity/average price directly once transactions exist.
