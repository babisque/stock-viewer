# M4 Spec: Data Import & Cutover

## 1. Overview and Goal
* **Purpose:** Move the user's real data from the Node app into the .NET app, prove nothing changed, and then remove the Node/TypeScript code so the repo is .NET-only.
* **Success Criteria:**
  * Every position and transaction from the Node app exists in the .NET DB with the **same ids, tickers, markets, notes, dates, quantities, prices, fees and timestamps**.
  * For every position, the .NET API returns the same quantity, average price and realized profit as the Node API did before the import. Terminating values are compared exactly, and repeating fractions after rounding to 10 decimal places. Portfolio totals match for the same quotes and rate.
  * The original Node DB file is untouched and a backup copy exists.
  * After the user confirms the parity report, `apps/`, `packages/` and all Node tooling files are deleted. `CLAUDE.md` and `specs/README.md` are rewritten for .NET, and `dotnet build -warnaserror` + `dotnet test` pass.
  * `aspire run` is the only command needed to run the app.

## 2. Tech Stack and Architecture
Same as M1–M3. The only addition is a temporary console app `tools/StockViewer.Import` (references `StockViewer.Shared` and the Api's `Data/`), with no new packages.

### Import plan (requires approval before running)
The import goes through the **Node API's JSON**, not the raw Prisma file. Prisma stored decimals in SQLite as 64-bit floats and used its own date encoding. The Node API already turns those into the exact strings the app showed to the user, so that output is the source of truth.

1. **Back up:** copy the Node DB (default `apps/api/prisma/dev.db`, or whatever `DATABASE_URL` points to; ask the user if unsure) to `*.db.bak`. If a .NET DB already exists, back it up too.
2. **Export:** with the Node API running, `tools/StockViewer.Import export --from http://localhost:3001 --out data/node-export.json` saves:
   * `GET /api/positions`
   * `GET /api/positions/{id}/transactions` for each position
   * `GET /api/portfolio` (for `realizedProfit` and the before-snapshot).
3. **Import:** `tools/StockViewer.Import import --file data/node-export.json --db <path>` checks these, in order:
   * Refuses to run if the target DB already has positions.
   * Validates every record with the `Shared` rules and stops on the first invalid one, writing nothing.
   * Inserts positions and transactions in **one** DB transaction, keeping `id`, `createdAt`, `updatedAt` and transaction `date`/`createdAt`.
   * Recomputes `Quantity`/`AveragePrice` mirror columns with `Ledger.Derive`.
4. **Verify:** `tools/StockViewer.Import verify --file data/node-export.json --to <dotnet-api-url>` compares the .NET API output with the export, field by field, and prints a parity report (OK / mismatch per position). Any mismatch means the import is rolled back by restoring the backup, and the cause is investigated.
5. **Confirm:** show the report to the user. Cutover starts only after explicit confirmation.

`data/` is git-ignored, because the export contains personal financial data.

### Cutover (after the user confirms parity)
1. Delete `apps/`, `packages/` and the Node tooling found by `git ls-files` at the repo root:
   * `package.json`, `package-lock.json`
   * `.nvmrc`, `.tool-versions` (or replace its `nodejs` entry with `dotnet`)
   * `.prettierrc`, `.prettierignore`
   * ESLint/TS/Vitest root configs, if any.
   * List the exact files to the user before deleting.
2. Delete `tools/StockViewer.Import` (it lives on in git history).
3. Rewrite `.gitignore` for .NET only (`bin/`, `obj/`, `*.db*`, `.env*`, `data/`, `TestResults/`, `.vs/`, `*.user`).
4. Rewrite `CLAUDE.md`, keeping its structure and rules but with .NET terms:
   * **Stack:** .NET 10, C# with nullable enabled, Aspire, minimal APIs, Blazor Web App (Interactive Server), EF Core + SQLite, xUnit v3 + bUnit.
   * **Projects:** `src/StockViewer.{AppHost,ServiceDefaults,Shared,Api,Web}`, `tests/…`
   * **Price sources:** unchanged, behind `IPriceProvider` in `src/StockViewer.Api/Prices/`.
   * **Run commands:** `aspire run`, `dotnet test`, `dotnet build -warnaserror`, `dotnet ef migrations add <Name> --project src/StockViewer.Api`.
   * **Conventions:** static pure functions over classes, `decimal` only for money (never `double`), `Money` carries currency, profit calculations live in `StockViewer.Shared` with tests, every feature has a test.
   * **Off-limits** and **Fallbacks:** unchanged.
5. Update `specs/README.md`: mark M1–M4 done, note that Phases 1–4 describe behaviour now implemented in .NET, and keep the old phase specs as history with a header note ("Implemented in Node; ported to .NET in `specs/migration/`").
6. Open a PR `blazorMigration → main` only when the user asks.

## 3. Executable Commands
* **Back up:** `cp apps/api/prisma/dev.db apps/api/prisma/dev.db.bak`
* **Export:** `npm run dev -w apps/api`, then `dotnet run --project tools/StockViewer.Import -- export --from http://localhost:3001 --out data/node-export.json`
* **Import:** `dotnet ef database update --project src/StockViewer.Api`, then `dotnet run --project tools/StockViewer.Import -- import --file data/node-export.json --db src/StockViewer.Api/data/stockviewer.db`
* **Verify:** `aspire run`, then `dotnet run --project tools/StockViewer.Import -- verify --file data/node-export.json --to <api-url>`
* **After cutover:** `dotnet build -warnaserror && dotnet test && aspire run`

## 4. Project Structure
```
tools/StockViewer.Import/     # temporary; deleted at the end of M4
├── Program.cs                # export | import | verify
├── Export.cs
├── Import.cs
└── Verify.cs
tests/StockViewer.Import.Tests/   # temporary; deleted with the tool
```
Final repo after cutover:
```
global.json  StockViewer.slnx  Directory.Build.props  Directory.Packages.props  .editorconfig  .gitignore  CLAUDE.md
src/   tests/   specs/
```

## 5. Code Style and Conventions
* Same as M1–M3.
* `Import` and `Verify` are pure functions over the export model plus a thin I/O shell, and they're tested with small fixture exports:
  * multiple buys, partial sell, full sell then rebuy, fees, fractional US, notes with unicode;
  * a deliberately broken export that must be rejected without writing anything.

## 6. Boundaries and Guardrails
* **Always do:**
  * Back up both DBs before importing.
  * Show the parity report and the list of files to delete before deleting anything.
  * Keep the original Node DB file untouched.
* **Ask first:**
  * Before running the import (present this plan and the backup location).
  * Before deleting the Node code (after showing the parity report).
  * Before rewriting `CLAUDE.md` (show the diff).
  * Before merging or opening a PR to `main`.
* **Never do:**
  * Never delete or modify the original Prisma DB or its backup.
  * Never commit the export JSON or any `.db` file.
  * Never "fix" data during the import. If something doesn't validate, stop and report it.
  * Never touch `.env` files or tokens. The user moves `BRAPI_TOKEN` to user-secrets themselves (`dotnet user-secrets set Brapi:Token <value> --project src/StockViewer.Api`).
