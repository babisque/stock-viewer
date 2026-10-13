# stock-viewer — Migration to .NET (roadmap)

The Node/TypeScript app (Phases 1–4) is being rewritten on .NET 10:
- **Aspire** orchestrates the projects.
- **Minimal APIs** serve the backend.
- A **Blazor Web App** ("Blazor United", Interactive Server render mode) is the frontend.

The port is about **behaviour parity**. It adds no new features. Each migration phase has its own spec, and a phase starts only after the previous one meets its success criteria.

| Phase | Spec | Goal | Status |
|---|---|---|---|
| M1 | [m1-foundation.md](m1-foundation.md) | Solution + Aspire scaffold, shared domain library (money, calculations, validation, contracts), EF Core schema | Not started |
| M2 | [m2-api-parity.md](m2-api-parity.md) | Minimal API with the exact same REST contract as the Node API, price providers, FX, cache | Not started |
| M3 | [m3-blazor-ui.md](m3-blazor-ui.md) | Blazor UI with the same features and UX rules as the React app | Not started |
| M4 | [m4-cutover.md](m4-cutover.md) | Import existing data, remove the Node code, rewrite CLAUDE.md | Not started |

## Decisions (already taken, don't revisit without asking)
- **Database:** SQLite + EF Core. No database container. Aspire runs only `api` and `web`.
- **Render mode:** Interactive Server. All API calls happen on the server (Web → Api through Aspire service discovery). The browser talks only to the Web app over SignalR. There is no CORS, no WASM and no `.Client` project.
- **UI:** hand-written Razor components and plain CSS with light/dark design tokens. The only UI library is Blazor-ApexCharts, used for the allocation chart.
- **Strategy:** side-by-side. The .NET solution lives in `src/` and `tests/` at the repo root, next to the untouched `apps/` and `packages/`. The Node app keeps working until M4 deletes it.

## Target architecture

```
stock-viewer/
├── global.json                       # pins the .NET 10 SDK
├── StockViewer.slnx
├── Directory.Build.props             # Nullable, TreatWarningsAsErrors, ImplicitUsings, LangVersion
├── Directory.Packages.props          # central package management, exact versions
├── .editorconfig
├── src/
│   ├── StockViewer.AppHost/          # Aspire app model: api + web
│   ├── StockViewer.ServiceDefaults/  # OpenTelemetry, health checks, service discovery, HttpClient resilience
│   ├── StockViewer.Shared/           # ← packages/shared  (no ASP.NET dependency)
│   ├── StockViewer.Api/              # ← apps/api         (minimal API, EF Core, prices, fx)
│   └── StockViewer.Web/              # ← apps/web         (Blazor Web App, Interactive Server)
└── tests/
    ├── StockViewer.Shared.Tests/
    ├── StockViewer.Api.Tests/
    └── StockViewer.Web.Tests/
```

```
Browser ──SignalR──▶ StockViewer.Web ──HTTP (https+http://api)──▶ StockViewer.Api ──▶ SQLite (EF Core)
                                                                      └──▶ Yahoo / brapi / AwesomeAPI
```

Dependency rule: `Api` and `Web` both reference `Shared`. `Shared` references nothing but the BCL, and `Web` never references `Api`.

## TS → .NET mapping ("lightweight tools")
| Today | .NET | Why |
|---|---|---|
| Fastify + fastify-type-provider-zod | Minimal APIs, `MapGroup("/api")`, `TypedResults` | Built in, no framework on top |
| Zod schemas (validation + types) | `record` DTOs + pure `Validate*` functions in `Shared` | One set of rules for the API and Blazor forms. No FluentValidation |
| `decimal.js`, decimal strings in JSON | C# `decimal` + `DecimalStringJsonConverter` | `decimal` is base-10 and exact. JSON stays `"28.45"` |
| "every value carries its currency" | `readonly record struct Money(decimal Amount, Currency Currency)` | Mixing currencies throws, so the rule is enforced in code |
| Prisma + SQLite (`better-sqlite3`) | EF Core 10 + `Microsoft.EntityFrameworkCore.Sqlite` | The EF SQLite provider stores `decimal` as TEXT, which is exact (Prisma stored REAL) |
| `yahoo-finance2` | Typed `HttpClient` against Yahoo's public endpoints | There is no maintained official .NET SDK. See M2 |
| `fetch` (brapi, AwesomeAPI) | Typed `HttpClient`s, `System.Text.Json` records validated before use | Built in |
| `cache.ts` (TTL `Map`) | Small `TtlCache<T>` using `TimeProvider` | Tests use `FakeTimeProvider` instead of `now()` injection |
| Fastify/pino logs | `ILogger` + OpenTelemetry → Aspire dashboard | Comes with ServiceDefaults |
| msw | `StubHttpMessageHandler` in the test project | No dependency |
| Vitest, Testing Library | xUnit v3, bUnit, `Microsoft.AspNetCore.Mvc.Testing` | Uses the built-in `Assert` |
| TanStack Query `refetchInterval` | `PeriodicTimer` in the dashboard + `visibilitychange` JS interop | |
| localStorage preferences | Cookies `sv-currency`, `sv-theme` | Prerender can read them, so the theme doesn't flash |
| shadcn/ui + Tailwind | Razor components + scoped CSS (`*.razor.css`) + tokens in `app.css` | No CSS build step |
| Recharts | Blazor-ApexCharts | Small, MIT, donut + legend |
| TanStack Table | Hand-written sort/filter using `Shared` comparers | |
| React Hook Form | `EditForm` + a validator component that calls the `Shared` rules | |

## Contract parity (applies to every phase)
The REST API keeps **exactly** the routes, status codes, JSON field names, JSON types and error codes of the Node API. CLAUDE.md: "never rename public APIs". Changing anything in this table requires asking first.

| Method | Route | Success | Errors |
|---|---|---|---|
| GET | `/api/health` | `200 { status: "ok" }` | — |
| GET | `/api/positions` | `200 Position[]` (B3 first, then US; ticker A→Z) | — |
| POST | `/api/positions` | `201 Position` | `400 VALIDATION_ERROR`, `409 DUPLICATE_POSITION` |
| PUT | `/api/positions/{id}` | `200 Position` | `400`, `404 POSITION_NOT_FOUND`, `409 DERIVED_VALUE_READ_ONLY` |
| DELETE | `/api/positions/{id}` | `204` | `404 POSITION_NOT_FOUND` |
| GET | `/api/positions/{id}/transactions` | `200 Transaction[]` (application order) | `404 POSITION_NOT_FOUND` |
| POST | `/api/positions/{id}/transactions` | `201 Position` | `400`, `404`, `409 INSUFFICIENT_QUANTITY` |
| DELETE | `/api/transactions/{id}` | `204` | `404 TRANSACTION_NOT_FOUND`, `409 INSUFFICIENT_QUANTITY` |
| GET | `/api/portfolio` | `200 Portfolio` | — |
| POST | `/api/portfolio/refresh` | `200 Portfolio` (caches cleared first) | — |
| GET | `/api/fx/usd-brl` | `200 FxRate` | `503 FX_UNAVAILABLE` |
| any | unknown route under `/api` | — | `404 NOT_FOUND` |

- Error body is always `{ "error": { "code": "<ErrorCode>", "message": "<text>" } }`, and `500` returns `INTERNAL_ERROR`. It is never `ProblemDetails`.
- JSON uses camelCase. Enums are serialized as strings (`"B3"`, `"BUY"`). Money and quantities are **strings**, dates are `YYYY-MM-DD`, and timestamps are ISO 8601 UTC.

## Shared definitions (carried over unchanged)
- **Market:** `B3` (currency `BRL`) or `US` (currency `USD`). The currency is always derived from the market.
- **Ticker:** stored without suffix (`PETR4`, `AAPL`). Provider symbols (`PETR4.SA`) are built only inside providers.
- **Money:** `decimal` in code, decimal **strings** in JSON. Never `double`/`float`. Round only when formatting for display.

## Coverage map (Node module → migration phase)
| Node source | .NET target | Phase |
|---|---|---|
| `packages/shared/src/schemas/*` (market, decimal, position, transaction, portfolio, api) | `Shared/Contracts`, `Shared/Validation` | M1 |
| `packages/shared/src/money/*` (invested, profit, convert, average-price, allocation, sort, format) | `Shared/Money` | M1 |
| `apps/api/prisma/schema.prisma` | `Api/Data/StockViewerDb.cs` + EF migration | M1 (proposal → approval) |
| `apps/api/src/routes/health.ts` | `Api/Endpoints/HealthEndpoints.cs` | M1 |
| `apps/api/src/app.ts`, `lib/errors.ts` | `Api/Errors/*`, `Program.cs` | M2 |
| `apps/api/src/routes/positions.ts`, `positions/positions.ts` | `Api/Endpoints/PositionEndpoints.cs`, `Api/Positions/Positions.cs` | M2 |
| `apps/api/src/routes/transactions.ts`, `transactions/*` | `Api/Endpoints/TransactionEndpoints.cs`, `Api/Transactions/*` | M2 |
| `apps/api/src/prices/*` | `Api/Prices/*` | M2 |
| `apps/api/src/fx/usd-brl.ts` | `Api/Fx/UsdBrl.cs` | M2 |
| `apps/api/src/routes/portfolio.ts`, `portfolio/build-portfolio.ts` | `Api/Endpoints/PortfolioEndpoints.cs`, `Api/Portfolio/BuildPortfolio.cs` | M2 |
| `apps/web/src/lib/*` (api client, display) | `Web/Api/ApiClient.cs`, `Web/Display/Display.cs` | M3 |
| `apps/web/src/components/ui/*` | `Web/Components/Ui/*` | M3 |
| `apps/web/src/features/positions/*` | `Web/Features/Positions/*` | M3 |
| `apps/web/src/features/transactions/*` | `Web/Features/Transactions/*` | M3 |
| `apps/web/src/features/dashboard/*` | `Web/Features/Dashboard/*` | M3 |
| `apps/web/src/features/preferences/*` | `Web/Features/Preferences/*` | M3 |
| `apps/api/prisma/dev.db` (data) | Import into the new DB | M4 |
| `apps/`, `packages/`, npm config, `CLAUDE.md` | Deleted / rewritten | M4 |

## Rules that hold in every migration phase
- Port behaviour, don't redesign it. If the Node behaviour looks wrong, write it down and ask. Don't "fix" it during the port.
- Don't implement Phase 5+ or "Future ideas" features (dividends, IR helper, history chart, alerts).
- Every new class/function/endpoint/component gets a test. Port the matching Node test cases first, then add .NET-specific ones.
- Ask before adding a NuGet package that the phase spec doesn't list.
- Never touch `.env` files, user-secrets values or API tokens. Never modify the Node app's Prisma schema or migrations.
- Any EF Core migration needs a written plan approved first (CLAUDE.md off-limits rule).
